using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// バトルのキャラクターを制御するベースクラス.
/// </summary>
public class BattleCharacterController : BattleObjectController
{
    #region フィールド/プロパティ（インスペクター用）

    /// <summary>
    /// 攻撃情報一覧.
    /// </summary>
    public List< AttackOrder > attackOrderList = new List< AttackOrder >();

    /// <summary>
    /// ゾーンヒット.
    /// </summary>
    public ZoneHitReceiver zoneHitReceiver;

    #endregion

    #region フィールド/プロパティ（public）

    /// <summary>
    /// 攻撃スタイル.
    /// </summary>
    public AttackStyleType AttackStyle { get; set; }

    /// <summary>
    /// AIへの参照.
    /// </summary>
    /// <value>A.</value>
    public BattleCharacterAI AI { get; protected set; }

    /// <summary>
    /// ターゲッティング.
    /// </summary>
    public TargetingStatus Targeting { get; protected set; }

    /// <summary>
    /// ターゲット中の味方.
    /// </summary>
    public BattleObjectController FriendTarget { get; set; }

    /// <summary>
    /// ターゲット中の位置.
    /// </summary>
    /// <remarks>
    /// 「ターゲット位置なし」の判定のため,nullを許容.
    /// </remarks>
    public Vector2? TargetPosition { get; set; }

    /// <summary>
    /// 現在の移動方向.
    /// </summary>
    public Vector2 CurrMoveAngle { get; protected set; }

    /// <summary>
    /// ターゲットとなる方向.
    /// </summary>
    public Vector2 TargetAngle { get; set; }

    /// <summary>
    /// 方向を更新中か.
    /// </summary>
    public bool IsUpdateAngle { get{ return ( CurrMoveAngle != TargetAngle ); } }

    /// <summary>
    /// 移動先の位置.
    /// </summary>
    public Vector2 MovePoint { get; set; }

    /// <summary>
    /// 現在の移動スタイル.
    /// </summary>
    public MoveStyle CurrMoveStyle { get; set; }

    /// <summary>
    /// オート移動中か.
    /// </summary>
    public bool IsAutoMove { get; set; }

    /// <summary>
    /// オート攻撃中か.
    /// </summary>
    public bool IsAutoAttack { get; set; }

    /// <summary>
    /// 行動制限中か.※主にスキルの使用で変動.
    /// </summary>
    public bool IsRestrictAction { get; set; }

    /// <summary>
    /// 現在のオート攻撃インデックス.
    /// </summary>
    public int CurrAutoAttackIndex { get; set; }

    /// <summary>
    /// 現在の移動速度.
    /// ( 移動速度 + 状態による補正 )
    /// </summary>
    public virtual float CurrMoveSpeed { get{ return ( Status.MoveSpeed + condition.GetMoveSpeedFluctuation() ); } }

    /// <summary>
    /// 現在の攻撃情報.
    /// </summary>
    public AttackOrder CurrAttackOrder { get{ return ( attackOrderList.Count <= 0 ) ? null : attackOrderList[ CurrAutoAttackIndex ]; } }

    /// <summary>
    /// 移動スタイル.
    /// </summary>
    public enum MoveStyle
    {
        /// <summary>
        /// 指定位置へ移動.
        /// </summary>
        TO_POINT,

        /// <summary>
        /// 指定キャラへ移動.
        /// </summary>
        TO_TARGET_CHARA,
    }

    #endregion

    #region フィールド/プロパティ

    /// <summary>
    /// 移動処理で使用するVector2のキャッシュ.
    /// </summary>
    protected Vector2 moveVector = new Vector2();

    /// <summary>
    /// オート攻撃のインターバル.
    /// </summary>
    protected float autoAttackInterval;

    /// <summary>
    /// オート移動の停止を判断する距離.
    /// NOTE: ターゲットとの距離がこれ以下になると停止する.
    /// </summary>
    protected float autoMoveStopDistance = DEFAULT_AUTO_MOVE_STOP_DISTANCE;

    #endregion

    #region 定数

    /// <summary>
    /// 移動速度の係数.
    /// 「移動速度」パラメータにこの係数をかけたものが,実際の移動速度.
    /// </summary>
    protected const float MOVE_SPEED_COEFFICIENT = 0.01f;

    /// <summary>
    /// 移動方向を更新する速度.
    /// </summary>
    protected const float UPDATE_ANGLE_SPEED = 5f;

    /// <summary>
    /// オート移動の停止を判断する距離のデフォルト値.
    /// </summary>
    protected const float DEFAULT_AUTO_MOVE_STOP_DISTANCE = 0.01f;

    /// <summary>
    /// ライン描画時のZ座標.
    /// </summary>
    protected const float LINE_POSITION_Z = -1f;

    #endregion

    #region イベント

    /// <summary>
    /// 移動を終了したときに実行するイベント.
    /// </summary>
    public Action MoveEndEvent { get; set; }

    #endregion

    #region メソッド

    /// <summary>
    /// インスタンス生成時に一度だけ実行.
    /// </summary>
    protected override void Awake()
    {
        AI = this.GetComponent< BattleCharacterAI >();
        Targeting = this.GetComponent< TargetingStatus >();
        TargetPosition = null;

        if( zoneHitReceiver != null ) zoneHitReceiver.Setup( this );

        base.Awake();
    }

    /// <summary>
    /// 毎フレーム処理.
    /// </summary>
    protected override void Update()
    {
        base.Update();

        UpdateAutoMove();
        UpdateAngle();
        // オート攻撃の更新.
        UpdateAutoAttack();
    }

    /// <summary>
    /// 毎フレーム処理（Physics系はこちらを使用）.
    /// </summary>
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
    }

    /// <summary>
    /// セットアップ.
    /// </summary>
    public override void Setup()
    {
        base.Setup();

        EnableCondition = true;
        CurrMoveAngle = Vector2.up;
        TargetAngle = Vector2.up;

        AttackStyle = AttackStyleType.GUNNER;

        if( AI != null ) AI.Setup( this );
    }

    #endregion

    #region 待機関連

    /// <summary>
    /// 待機状態にする.
    /// </summary>
    public virtual void SetStay()
    {
        if( AI != null )
        {
            AI.SetStayMode();
        }
    }

    #endregion

    #region 移動関連

    /// <summary>
    /// 移動方向を更新.
    /// </summary>
    protected virtual void UpdateAngle()
    {
        if( CurrMoveAngle == TargetAngle ) return;
        if( IsDead || IsSpecialDamage || condition.IsImmobile ) return;

        // CurrMoveAngle = Vector2.Lerp( CurrMoveAngle, TargetAngle, 0.1f );
        // Mathf.MoveTowardsAngle
        CurrMoveAngle = Vector2.MoveTowards( CurrMoveAngle, TargetAngle, UPDATE_ANGLE_SPEED * Time.deltaTime );
    }

    /// <summary>
    /// 移動を停止する.
    /// </summary>
    public virtual void MoveStop()
    {
        IsAutoMove = false;

        if( MoveEndEvent != null ) MoveEndEvent();
    }

    /// <summary>
    /// 移動先を設定してオート移動を開始する.
    /// ※AIからのみ呼び出す.
    /// </summary>
    /// <param name="movePoint"> 移動先ポイント（ワールド）. </param>
    public virtual void SetMovePoint( Vector2 movePoint )
    {
        // 移動先をセット.
        MovePoint = movePoint;

        // ターゲットの向きを更新.
        TargetAngle = ( MovePoint - new Vector2( this.transform.position.x, this.transform.position.y ) ).normalized;

        // 停止する距離を更新.
        autoMoveStopDistance = DEFAULT_AUTO_MOVE_STOP_DISTANCE;

        CurrMoveStyle = MoveStyle.TO_POINT;
        IsAutoMove = true;
    }

    /// <summary>
    /// 移動先となるキャラを設定してオート移動を開始する.
    /// ※AIからのみ呼び出す.
    /// </summary>
    /// <param name="chara"> 移動先のターゲットとなるキャラ. </param>
    public virtual void SetMovePoint( BattleObjectController chara )
    {
        if( Targeting == null || chara == null ) return;

        // 移動先をセット.
        Targeting.TargetObject = chara;
        MovePoint = new Vector2( chara.transform.position.x, chara.transform.position.y );

        // 停止する距離を更新.
        autoMoveStopDistance = chara.objectSize + this.objectSize;

        CurrMoveStyle = MoveStyle.TO_TARGET_CHARA;
        IsAutoMove = true;
    }

    /// <summary>
    /// 移動状態にする.
    /// </summary>
    /// <param name="movePoint"> 移動先ポイント（ワールド）. </param>
    public virtual void SetMoveMode( Vector2 movePoint )
    {
        AI.SetMoveMode( movePoint );
    }

    /// <summary>
    /// 移動状態にする.
    /// </summary>
    /// <param name="target"> 移動先のターゲット. </param>
    public virtual void SetMoveMode( BattleObjectController target )
    {
        AI.SetMoveMode( target );
    }

    #endregion

    #region 攻撃関連

    /// <summary>
    /// オート攻撃の更新.
    /// </summary>
    protected virtual void UpdateAutoAttack()
    {
        if( !IsAutoAttack || IsRestrictAction || IsDead || IsSpecialDamage || condition.IsImmobile ) return;

        // ターゲットが外れたら攻撃を停止.
        if( Targeting.TargetObject == null )
        {
            IsAutoAttack = false;
            return;
        }

        if( autoAttackInterval <= 0f )
        {
            Attack( CurrAutoAttackIndex );

            // インターバルを更新. ( 攻撃間隔 - 状態による補正 )
            autoAttackInterval = Status.AutoAttackDelay - condition.GetAutoAttackDelayFluctuation();
        }
        else
        {
            autoAttackInterval -= Time.deltaTime;
        }
    }

    /// <summary>
    /// 現在の攻撃設定でオート攻撃を開始する.
    /// </summary>
    public virtual void PlayAutoAttack()
    {
        PlayAutoAttack( CurrAutoAttackIndex );
    }

    /// <summary>
    /// オート攻撃を開始する.
    /// </summary>
    /// <param name="attackIndex"> 攻撃情報一覧のインデックス. </param>
    public virtual void PlayAutoAttack( int attackIndex )
    {
        // インデックスが一覧の個数以上なら処理をしない.
        if( attackIndex >= attackOrderList.Count ) return;

        CurrAutoAttackIndex = attackIndex;
        IsAutoAttack = true;
    }

    /// <summary>
    /// オート攻撃を停止する.
    /// </summary>
    public virtual void StopAutoAttack()
    {
        IsAutoAttack = false;
        autoAttackInterval = 0f;
    }

    /// <summary>
    /// 攻撃対象サーチ状態にする.
    /// </summary>
    /// <param name="searchTime"> サーチ時間. </param>
    public virtual void SetBewareMode( float searchTime )
    {
        AI.SetBewareMode( searchTime );
    }

    /// <summary>
    /// 攻撃用オート移動状態にする.
    /// </summary>
    /// <param name="target"> ターゲット. </param>
    public virtual void SetAimingMode( BattleObjectController target )
    {
        StopAutoAttack();
        AI.SetAimingMode( target );
    }

    #endregion

    #if UNITY_EDITOR

    [ ContextMenu( "Attack Test" ) ]
    public void AttackTest()
    {
        Attack( 0 );
    }

    #endif
}
