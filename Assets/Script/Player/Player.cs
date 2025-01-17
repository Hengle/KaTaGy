﻿using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.Events;
using DG.Tweening;

[RequireComponent(typeof(NavMeshAgent))]
public class Player : Photon.MonoBehaviour
{
    #region 取得單例
    private MatchTimer matchTime;
    public MatchTimer MatchTimeManager { get { if (matchTime == null) matchTime = MatchTimer.Instance; return matchTime; } }

    private HintManager hintManager;
    public HintManager HintScript { get { if (hintManager == null) hintManager = HintManager.instance; return hintManager; } }

    private FloatingTextController floatTextCon;
    protected FloatingTextController FloatTextCon { get { if (floatTextCon == null) floatTextCon = FloatingTextController.instance; return floatTextCon; } }

    private AudioManager audioScript;
    public AudioManager AudioScript { get { if (audioScript == null) audioScript = AudioManager.instance; return audioScript; } }

    private BuildManager buildScript;
    public BuildManager BuildScript { get { if (buildScript == null) buildScript = BuildManager.instance; return buildScript; } }

    private PlayerObtain moneyScript;
    private PlayerObtain MoneyScript { get { if (moneyScript == null) moneyScript = PlayerObtain.instance; return moneyScript; } }
    #endregion

    #region 數據
    public GameManager.meIs meIs;
    public PlayerData.PlayerDataBase playerData;
    public PlayerData.PlayerDataBase originalData;
    private bool firstGetData = true;
    #endregion

    #region 血量相關
    [Header("左上螢幕UI")]
    private Image leftTopHpBar;
    private Image leftTopPowerBar;
    //角色頭上血量
    public Image UI_HpBar;
    private Animator ani;
    [Header("改變顏色")]
    private float maxValue;
    [SerializeField] Renderer myRender;
    #endregion

    private Transform myCachedTransform;
    //音效
    public AudioSource myAudio;
    public PlayerAni AniControll;
    [HideInInspector] public isDead deadManager;
    private Ray ray;
    private RaycastHit hit;
    public bool lockDodge;
    private bool canDodge = true;
    
    private NavMeshAgent nav;

    [SerializeField] GameObject clickPointPos;
    [SerializeField] LayerMask canClickToMove_Layer;

    //偵測目前跑步動畫
    private bool isRunning;
    public bool getIsRunning { get { return isRunning; } private set { isRunning = value; } }

    private bool stopClick;
    public bool StopClick { get { return stopClick; } set { stopClick = value; } }

    public CreatPoints MyCreatPoints;
    public PhotonView Net;
    //方向
    public Transform arrow; //滑鼠目標方向
    private Vector3 tmpMousePos;//滑鼠方向用(暫存)
    private Vector3 worldDir;//滑鼠方向用(暫存)
    private Vector3 mousePosition;//滑鼠正確點位
    private Vector3 tmpMousePoint;//找到滑鼠正確點位用(暫存)
    private Camera myMainCamera;
    [SerializeField] Projector arrowProjector;

    public UnityEvent skill_Q;
    public UnityEvent skill_W;
    public UnityEvent skill_E;
    public UnityEvent skill_R;

    public UnityEvent cancelSkill;

    #region 狀態
    public enum statesData
    {
        None,
        canMove_Atk,
        canMvoe_Build,
        notMove,
        Combo
    }
    private statesData myState = statesData.canMove_Atk;
    public statesData MyState { get { return myState; } set { myState = value; } }

    public enum SkillData
    {
        None,
        skill_Q,
        skill_W,
        skill_E,
        skill_R
    }
    private SkillData skillState = SkillData.None;
    public SkillData SkillState { get { return skillState; } set { skillState = value; } }

    private bool nowCC;
    public bool NowCC { get { return nowCC; } set { nowCC = value; } }
    #endregion
    private CapsuleCollider CharaCollider;
    //技能
    [HideInInspector]
    public SkillBase skillManager;
    [HideInInspector]
    public bool canSkill_Q = true;
    [HideInInspector]
    public bool canSkill_W = true;
    [HideInInspector]
    public bool canSkill_E = true;
    [HideInInspector]
    public bool canSkill_R = true;

    //負面
    private Tweener flyUp;

    #region 移動所需變量
    private Vector3 tmpNextPos;
    [HideInInspector]
    public Quaternion CharacterRot;
    private Vector3 maxDisGap;
    #endregion

    #region 小地圖所需
    RectTransform map;
    private float mapX;
    private float mapY;
    #endregion

    private void Awake()
    {
        CharaCollider = GetComponent<CapsuleCollider>();
        Net = GetComponent<PhotonView>();
        myCachedTransform = this.transform;
    }

    private void Start()
    {
        if (firstGetData)
            FirstFormatData();
        FormatData();

        if (photonView.isMine)
        {
            checkCurrentPlay();
            MyCreatPoints.enabled = false;
            SceneObjManager.Instance.myPlayer = this;
        }
        else
        {
            MyCreatPoints.ProdecePoints();
            SceneObjManager.Instance.enemy_Player = this;
            this.enabled = false;
        }
    }

    #region 改變正確玩家(可以攻擊的對象)
    void checkCurrentPlay()
    {
        if (GameManager.instance.getMyPlayer() == GameManager.MyNowPlayer.player_1)
        {
            Net.RPC("changeLayer", PhotonTargets.All, 30);
            arrowProjector.gameObject.layer = 0;
            Net.RPC("changeMask_1", PhotonTargets.All,(int)GameManager.instance.Meis);

        }
        else if (GameManager.instance.getMyPlayer() == GameManager.MyNowPlayer.player_2)
        {
            Net.RPC("changeLayer", PhotonTargets.All, 31);
            arrowProjector.gameObject.layer = 0;
            Net.RPC("changeMask_2", PhotonTargets.All, (int)GameManager.instance.Meis);
        }
    }
    [PunRPC]
    public void changeMask_1(int _who)
    {
        GetComponent<PlayerAni>().canAtkMask = GameManager.instance.getPlayer1_Mask;
        meIs = ((GameManager.meIs)_who);
    }
    [PunRPC]
    public void changeMask_2(int _who)
    {
        GetComponent<PlayerAni>().canAtkMask = GameManager.instance.getPlayer2_Mask;
        meIs = ((GameManager.meIs)_who);
    }
    #endregion

    #region 恢復初始數據
    void FirstFormatData()
    {
        firstGetData = false;
        //
        map = GameObject.Find("smallMapContainer").GetComponent<RectTransform>();
        //
        skillManager = GetComponent<SkillBase>();
        clickPointPos = GameObject.Find("clickPointPos");
        nav = GetComponent<NavMeshAgent>();
        deadManager = GetComponent<isDead>();
        nav.updateRotation = false;
        MyCreatPoints = GetComponent<CreatPoints>();
        myAudio = GetComponent<AudioSource>();
        ani = GetComponent<Animator>();
        AniControll = GetComponent<PlayerAni>();        

        if (photonView.isMine)
        {
            if (leftTopPowerBar == null)
                leftTopPowerBar = GameObject.Find("mpBar_0020").GetComponent<Image>();

            myMainCamera = Camera.main;
        }
        originalData = PlayerData.instance.getPlayerData(meIs);
    }

    void FormatData()
    {
        if (!firstGetData)
        {
            playerData = originalData;
            if (photonView.isMine)
            {
                if (BuildScript.nowBuilding)
                    BuildScript.BuildSwitch();
                if (nav != null)
                    nav.speed = playerData.moveSpeed;

                leftTopPowerBar.fillAmount = 1;
            }

            if (AniControll != null)
                AniControll.WeaponChangePos(1);
            deadManager.NoDamage(false);
            CharaCollider.enabled = true;
        }
    }

    public void GoFormatData()
    {
        UI_HpBar.fillAmount = 1;
        if (photonView.isMine)
        {
            if (leftTopHpBar != null)
                leftTopHpBar.fillAmount = 1;
            else
                leftTopHpBar = GameObject.Find("hpBar_0022").GetComponent<Image>();
        }

        FormatData();
    }
    #endregion

    #region 升級
    #region 玩家
    [PunRPC]
    public void UpdataData(int _level,int _whatAbility)
    {
        switch (((UpdateManager.Myability) _whatAbility))
        {
            case (UpdateManager.Myability.Player_ATK):
                UpdataData_Atk(_level);
                break;
            case (UpdateManager.Myability.Player_DEF):
                UpdataData_Def(_level);
                break;
            case (UpdateManager.Myability.Skill_Q_Player):
                UpdataData_Skill_Q(_level);
                break;
            case (UpdateManager.Myability.Skill_W_Player):
                UpdataData_Skill_W(_level);
                break;
            case (UpdateManager.Myability.Skill_E_Player):
                UpdataData_Skill_E(_level);
                break;
            case (UpdateManager.Myability.Skill_R_Player):
                UpdataData_Skill_R(_level);
                break;
            default:
                break;
        }

        PlayerData.instance.ChangeMyData(meIs, originalData);
    }

    void UpdataData_Atk(int _level)
    {
        switch (_level)
        {
            case (1):
                if (originalData.ATK_Level != _level)
                {
                    originalData.ATK_Level = _level;
                    originalData.Atk_Damage += originalData.updateData.Add_atk1;
                    originalData.Atk_maxDamage += originalData.updateData.Add_atk1;
                    originalData.Ap_original += originalData.updateData.Add_ap1;
                    originalData.Ap_Max += originalData.updateData.Add_ap1;

                    playerData.Atk_Damage += originalData.updateData.Add_atk1;
                    playerData.Atk_maxDamage += originalData.updateData.Add_atk1;
                    playerData.Ap_original += originalData.updateData.Add_ap1;
                    playerData.Ap_Max += originalData.updateData.Add_ap1;
                }
                break;
            case (2):
                if (originalData.ATK_Level != _level)
                {
                    originalData.ATK_Level = _level;
                    originalData.Atk_Damage += originalData.updateData.Add_atk2;
                    originalData.Atk_maxDamage += originalData.updateData.Add_atk2;
                    originalData.Ap_original += originalData.updateData.Add_ap2;
                    originalData.Ap_Max += originalData.updateData.Add_ap2;

                    playerData.Atk_Damage += originalData.updateData.Add_atk2;
                    playerData.Atk_maxDamage += originalData.updateData.Add_atk2;
                    playerData.Ap_original += originalData.updateData.Add_ap2;
                    playerData.Ap_Max += originalData.updateData.Add_ap2;
                }
                break;
            case (3):
                if (originalData.ATK_Level != _level)
                {
                    originalData.ATK_Level = _level;
                    originalData.Atk_Damage += originalData.updateData.Add_atk3;
                    originalData.Atk_maxDamage += originalData.updateData.Add_atk3;
                    originalData.Ap_original += originalData.updateData.Add_ap3;
                    originalData.Ap_Max += originalData.updateData.Add_ap3;

                    playerData.Atk_Damage += originalData.updateData.Add_atk3;
                    playerData.Atk_maxDamage += originalData.updateData.Add_atk3;
                    playerData.Ap_original += originalData.updateData.Add_ap3;
                    playerData.Ap_Max += originalData.updateData.Add_ap3;
                }
                break;
            default:
                break;
        }
    }
    void UpdataData_Def(int _level)
    {
        switch (_level)
        {
            case (1):
                if (originalData.DEF_Level != _level)
                {
                    originalData.DEF_Level = _level;
                    originalData.def_base += originalData.updateData.Add_def1;
                    originalData.Hp_Max += originalData.updateData.Add_hp1;
                    originalData.Hp_original += originalData.updateData.Add_hp1;

                    playerData.def_base += originalData.updateData.Add_def1;
                    playerData.Hp_Max += originalData.updateData.Add_hp1;
                    playerData.Hp_original += originalData.updateData.Add_hp1;
                }
                break;
            case (2):
                if (originalData.DEF_Level != _level)
                {
                    originalData.DEF_Level = _level;
                    originalData.def_base += originalData.updateData.Add_def2;
                    originalData.Hp_Max += originalData.updateData.Add_hp2;
                    originalData.Hp_original += originalData.updateData.Add_hp2;

                    playerData.def_base += originalData.updateData.Add_def2;
                    playerData.Hp_Max += originalData.updateData.Add_hp2;
                    playerData.Hp_original += originalData.updateData.Add_hp2;
                }
                break;
            case (3):
                if (originalData.DEF_Level != _level)
                {
                    originalData.DEF_Level = _level;
                    originalData.def_base += originalData.updateData.Add_def3;
                    originalData.Hp_Max += originalData.updateData.Add_hp3;
                    originalData.Hp_original += originalData.updateData.Add_hp3;

                    playerData.def_base += originalData.updateData.Add_def3;
                    playerData.Hp_Max += originalData.updateData.Add_hp3;
                    playerData.Hp_original += originalData.updateData.Add_hp3;
                }
                break;
            default:
                break;
        }
    }
    void UpdataData_Skill_Q(int _level)
    { }
    void UpdataData_Skill_W(int _level)
    { }
    void UpdataData_Skill_E(int _level)
    { }
    void UpdataData_Skill_R(int _level)
    { }
    #endregion

    #region 士兵
    [PunRPC]
    public void UpdateSoldier(byte _level, int _whatAbility)
    {
        if (photonView.isMine)
            SceneObjManager.Instance.UpdataMySoldier(_level, _whatAbility);
        else
            SceneObjManager.Instance.UpdataClientSoldier(_level, _whatAbility);
    }
    #endregion

    #region 塔防
    [PunRPC]
    public void UpdateTower(byte _level, int _whatAbility)
    {
        if (photonView.isMine)
            SceneObjManager.Instance.UpdataMyTower(_level, _whatAbility);
        else
            SceneObjManager.Instance.UpdataClientTower(_level, _whatAbility);
    }
    #endregion
    #endregion

    public void NeedToUpdate()
    {
        if (!deadManager.checkDead)
        {
            if (leftTopPowerBar.fillAmount != 1)
                AddPower();
            
            CorrectDirection();

            if (MyState != statesData.None)
            {
                nowCanDo();
                ///
                if (Input.GetKeyDown(KeyCode.LeftShift))
                {
                    myCachedTransform.position = GetNowMousePoint(clickPointPos.transform);
                }
            }
        }

        if (Input.GetKeyDown("z"))
        {
            takeDamage(10f, Vector3.zero, true);
        }
    }

    #region 目前狀態執行→update
    void nowCanDo()
    {
        switch (MyState)
        {
            case statesData.canMove_Atk:
                DetectSkillBtn();
                if (Input.GetMouseButtonDown(1))
                    ClickPoint();
                CharacterRun();
                if (AniControll.comboIndex == 0)
                    CharacterAtk_F();
                Dodge_Btn();
                ATK_Build_Btn();
                break;
            case statesData.canMvoe_Build:
                if (Input.GetMouseButtonDown(1))
                    ClickPoint();
                BuildScript.NeedToUpdate();
                CharacterRun();
                ATK_Build_Btn();
                break;
            case statesData.notMove:
                Dodge_Btn();
                break;
            case statesData.Combo:
                DetectSkillBtn();
                CharacterAtk_F();
                Dodge_Btn();
                AniControll.DetectAtkRanage();
                break;
            default:
                break;
        }
    }
    #endregion

    #region 偵測按下
    //切換攻擊與建造模式
    private void ATK_Build_Btn()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (BuildScript.nowSelect && !StopClick && (AniControll.anim.GetCurrentAnimatorStateInfo(0).fullPathHash == AniControll.aniHashValue[24] ||
             AniControll.anim.GetCurrentAnimatorStateInfo(0).fullPathHash == AniControll.aniHashValue[25] || AniControll.anim.GetCurrentAnimatorStateInfo(0).IsName("build_Idle")
             || AniControll.anim.GetCurrentAnimatorStateInfo(0).IsName("build_run")))
            {
                if (SkillState != SkillData.None)
                    CancelNowSkill();

                BuildScript.BuildSwitch();
                StopClick = true;
            }
        }

    }
    //閃避
    private void Dodge_Btn()
    {
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            if (canDodge && !lockDodge)
            {
                if (SkillState != SkillData.None)
                    CancelNowSkill();

                if (ConsumeAP(10f, true))
                {
                    stopAnything_Switch(true);
                    Dodge_FCN();
                }
            }
        }
    }
    //按下F→目前為combo
    private void CharacterAtk_F()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (AniControll.canClick)
            {
                if (SkillState != SkillData.None)
                    CancelNowSkill();

                if (MyState != statesData.Combo)
                {
                    getIsRunning = false;
                    nav.ResetPath();
                    MyState = statesData.Combo;
                    myCachedTransform.forward = arrow.forward;
                }

                AniControll.TypeCombo(myCachedTransform.forward);
            }
        }
    }
    #endregion

    #region 玩家技能
    private void DetectSkillBtn()
    {
        Character_Skill_Q();
        Character_Skill_W();
        Character_Skill_E();
        Character_Skill_R();
    }
    //Q
    private void Character_Skill_Q()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (SkillState != SkillData.skill_Q && canSkill_Q && skillManager.nowSkill == SkillBase.SkillAction.None)
            {
                if (SkillState != SkillData.None)
                    CancelNowSkill();
                skillManager.Skill_Q_Click();
            }
        }

        if (SkillState == SkillData.skill_Q)
            skillManager.In_Skill_Q();    
    }
    //W
    private void Character_Skill_W()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (SkillState != SkillData.skill_W && canSkill_W && skillManager.nowSkill == SkillBase.SkillAction.None)
            {
                if (SkillState != SkillData.None)
                    CancelNowSkill();
                skillManager.Skill_W_Click();
            }
        }

        if (SkillState == SkillData.skill_W)
            skillManager.In_Skill_W();
    }
    //E
    private void Character_Skill_E()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (SkillState != SkillData.skill_E && canSkill_E && skillManager.nowSkill == SkillBase.SkillAction.None)
            {
                if (SkillState != SkillData.None)
                    CancelNowSkill();
                skillManager.Skill_E_Click();
            }
        }

        if (SkillState == SkillData.skill_E)
            skillManager.In_Skill_E();
    }
    //R
    private void Character_Skill_R()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (SkillState != SkillData.skill_R && canSkill_R && skillManager.nowSkill == SkillBase.SkillAction.None)
            {
                if (SkillState != SkillData.None)
                    CancelNowSkill();
                skillManager.Skill_R_Click();
            }
        }

        if (SkillState == SkillData.skill_R)
            skillManager.In_Skill_R();
    }
    #endregion

    #region 偵測點擊位置
    public Transform test;
    void ClickPoint()
    {
        if (!IsMap())//我新加的
        {
            ray = myMainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit, 165, canClickToMove_Layer))
            {
                clickPointPos.transform.position = hit.point;
                getTatgetPoint(clickPointPos.transform.position);
            }
        }
    }

    bool IsMap()
    {
        mapX = Input.mousePosition.x - (map.position.x - (map.rect.width * 0.5f));
        mapY = Input.mousePosition.y - (map.position.y - (map.rect.height * 0.5f));

        return (mapX < map.rect.width && mapX > 0 && mapY < map.rect.height && mapY > 0);
    }

    //無時無刻偵測滑鼠方向
    void CorrectDirection()
    {
        tmpMousePos = Input.mousePosition;//鼠标在屏幕上的位置坐标
        tmpMousePos.z = myMainCamera.WorldToScreenPoint(arrow.position).z;
        worldDir.x = myMainCamera.ScreenToWorldPoint(tmpMousePos).x;
        worldDir.z = myMainCamera.ScreenToWorldPoint(tmpMousePos).z;
        worldDir.y = arrow.position.y;
        arrow.LookAt(worldDir);
    }

    //取得滑鼠位置這此範圍內
    public bool GetRangeAnyPoint(Transform _objPos, Transform _originalPos, float _maxRange)
    {
        if (_objPos != null)
        {
            GetNowMousePoint(_objPos);
            ray = myMainCamera.ScreenPointToRay(Input.mousePosition);
            if (Vector3.SqrMagnitude(mousePosition - _originalPos.position) <= _maxRange * _maxRange)
            {
                if (Physics.Raycast(ray, out hit, 155, canClickToMove_Layer))
                {
                    _objPos.position = hit.point;
                    return true;
                }
                else
                {
                    _objPos.position = mousePosition;
                    return false;
                }
            }
            else
            {
                if (Physics.Raycast(ray, out hit, 170, canClickToMove_Layer))
                {
                    _objPos.position = _originalPos.position + ((hit.point - _originalPos.position).normalized * _maxRange);
                    return true;
                }
                else
                {
                    _objPos.position = _originalPos.position + ((mousePosition - _originalPos.position).normalized * _maxRange);
                    return false;
                }               
            }            
        }
        return false;
    }

    //取得滑鼠位置
    public Vector3 GetNowMousePoint(Transform _obj)
    {
        tmpMousePoint = Input.mousePosition;//鼠标在屏幕上的位置坐标
        tmpMousePoint.z = myMainCamera.WorldToScreenPoint(_obj.transform.position).z;
        mousePosition.x = myMainCamera.ScreenToWorldPoint(tmpMousePoint).x;
        mousePosition.z = myMainCamera.ScreenToWorldPoint(tmpMousePoint).z;
        mousePosition.y = _obj.transform.position.y;
        return mousePosition;
    }
    #endregion

    #region 得到移動終點位置
    public void getTatgetPoint(Vector3 tragetPoint)
    {
        nav.SetDestination(tragetPoint);
        if (!AniControll.anim.GetBool(AniControll.aniHashValue[2]))
        {
            getIsRunning = true;
            Net.RPC("Ani_Run", PhotonTargets.All, getIsRunning);
        }
    }
    #endregion

    #region 角色移動
    void CharacterRun()
    {
        if (!photonView.isMine || deadManager.checkDead)
            return;

        if (getIsRunning)
        {
            #region 尋找下一個位置方向
            tmpNextPos = nav.steeringTarget;
            tmpNextPos.y = myCachedTransform.localPosition.y;
            CharacterRot = Quaternion.LookRotation(tmpNextPos- myCachedTransform.localPosition);
            #endregion

            #region 判斷是否到最終目標點→否則執行移動
            maxDisGap = nav.destination - myCachedTransform.localPosition;
            if (maxDisGap.sqrMagnitude < playerData.stoppingDst * playerData.stoppingDst)
            {
                isStop();
            }
            else
            {
                myCachedTransform.rotation = Quaternion.Lerp(myCachedTransform.rotation, CharacterRot, playerData.rotSpeed);
            }
            #endregion
        }
    }
    #endregion

    #region 被攻擊
    public void beHit(Vector3 _dir)
    {
        CharacterRot = Quaternion.LookRotation(-_dir.normalized);
        myCachedTransform.rotation = CharacterRot;
        if (photonView.isMine)
        {
            AniControll.beOtherHit();
            MyState = statesData.notMove;
            lockDodge = false;
        }
    }
    #endregion

    #region 負面效果
    //暈眩 僵直
    [PunRPC]
    public void GetDeBuff_Stun(float _time)
    {
        if (deadManager.noCC)
            return;

        if (!deadManager.checkDead)
        {
            if (photonView.isMine)
            {
                stopAnything_Switch(true);
                CancelNowSkill();
            }
            NowCC = true;
            if (!AniControll.anim.GetBool(AniControll.aniHashValue[15]))
            {
                AniControll.anim.CrossFade(AniControll.aniHashValue[18], 0.02f, 0);
                AniControll.anim.SetBool(AniControll.aniHashValue[9], true);
            }
            MatchTimeManager.SetCountDownNoCancel(Recover_Stun, _time);
        }
    }
    //緩速
    public void GetDeBuff_Slow()
    {

    }
    //破甲
    public void GetDeBuff_DestoryDef()
    {

    }
    //燒傷
    public void GetDeBuff_Burn()
    {

    }
    //擊飛
    [PunRPC]
    public void pushOtherTarget()
    {
        if (deadManager.noCC)
            return;

        if (!deadManager.checkDead)
        {
            CancelNowSkill();
            stopAnything_Switch(true);
            NowCC = true;
            if (!AniControll.anim.GetBool(AniControll.aniHashValue[15]))
            {
                AniControll.anim.CrossFade(AniControll.aniHashValue[19], 0.02f, 0);
            }
        }
    }

    //往上擊飛
    [PunRPC]
    public void HitFlayUp(float _damage,float _stunTime)
    {
        if (deadManager.noCC)
            return;
        stopAnything_Switch(true);
        if (!NowCC)
            GetDeBuff_Stun(_stunTime);
        flyUp = myCachedTransform.DOMoveY(myCachedTransform.position.y + 6, 0.3f).SetAutoKill(false).SetEase(Ease.OutBack);
        takeDamage(_damage, Vector3.zero, false);
        flyUp.onComplete = delegate () { EndFlyUp(); };
    }
    #endregion

    #region 負面狀態恢復
    //恢復暈眩
    void Recover_Stun()
    {
        GoBack_AtkState();
        AniControll.anim.SetBool(AniControll.aniHashValue[9], false);
    }
    //回到地上
    void EndFlyUp()
    {
        flyUp.PlayBackwards();
    }
    #endregion

    #region 功能
    //消耗能量
    public bool ConsumeAP(float _value, bool _nowConsumer)
    {
        if (playerData.Ap_original - _value >= 0)
        {
            if (_nowConsumer)
            {
                playerData.Ap_original -= _value;
                leftTopPowerBar.fillAmount = playerData.Ap_original / playerData.Ap_Max;
            }
            return true;
        }
        else
        {
            HintScript.CreatHint("能量不足");
            return false;
        }
    }
    private void AddPower()
    {
        playerData.Ap_original += playerData.add_APValue * Time.deltaTime;
        playerData.Ap_original = Mathf.Clamp(playerData.Ap_original, 0, playerData.Ap_Max);
        leftTopPowerBar.fillAmount = playerData.Ap_original / playerData.Ap_Max;
    }
    //閃避執行
    private void Dodge_FCN()
    {
        canDodge = false;
        myCachedTransform.forward = arrow.forward;
        Net.RPC("GoDodge", PhotonTargets.All);
        MatchTimeManager.SetCountDownNoCancel(Dodge_End, playerData.Dodget_Delay);
    }
    //閃避cd結束
    void Dodge_End()
    {
        canDodge = true;
    }

    #region 技能相關
    //技能冷卻時間
    public void CountDown_Q()
    {
        canSkill_Q = true;
    }
    public void CountDown_W()
    {
        canSkill_W = true;
    }
    public void CountDown_E()
    {
        canSkill_E = true;
    }
    public void CountDown_R()
    {
        canSkill_R = true;
    }

    public void CancelNowSkill()
    {
        switch (SkillState)
        {
            case SkillData.skill_Q:
                canSkill_Q = true;               
                break;
            case SkillData.skill_W:
                canSkill_W = true;
                break;
            case SkillData.skill_E:
                canSkill_E = true;                
                break;
            case SkillData.skill_R:
                canSkill_R = true;
                break;
            default:
                break;
        }
        skillManager.CancelDetectSkill(SkillState);
        SkillState = SkillData.None;
    }
    #endregion
    #endregion

    #region 其他腳本需求
    //停止角色移動
    public void isStop()
    {
        if (AniControll.anim.GetBool(AniControll.aniHashValue[2]))
        {
            getIsRunning = false;
            nav.ResetPath();
            Net.RPC("Ani_Run", PhotonTargets.All, getIsRunning);
        }
    }

    //停止一切行為(無法操控)
    public void stopAnything_Switch(bool _stop)
    {
        isStop();
        if (_stop)
        {
            AniControll.StopComboAudio();
            MyState = statesData.None;
        }
        else
        {
            if (!BuildScript.nowBuilding)
                MyState = statesData.canMove_Atk;
            else
                MyState = statesData.canMvoe_Build;           
        }
    }
    //停止行動 只能閃避
    public void StopAllOnlyDodge()
    {
        isStop();
        MyState = statesData.notMove;
        lockDodge = false;
    }
    //回攻擊狀態
    public void GoBack_AtkState()
    {
        if (!NowCC)
            MyState = statesData.canMove_Atk;
    }

    //切換目前模式(攻擊 , 建造)
    public void switchWeapon(bool _can)
    {
        if (photonView.isMine && !deadManager.checkDead)
        {
            Net.RPC("weaponOC", PhotonTargets.All, _can);
        }
    }
    //等待建造時間
    public void switchScaffolding(bool _t)
    {
        if (photonView.isMine)
            Net.RPC("waitBuild", PhotonTargets.All, _t);
    }

    //獲得金錢
    public void GetSomeMoney(int _money)
    {
        if (photonView.isMine)
            MoneyScript.obtaniResource(_money);
    }

    //精靈王傳送用
    public void TeleportPos(Vector3 _pos)
    {
        nav.Warp(_pos);
    }
    #endregion

    #region 偵測目前是否有路徑
    public bool getNavPath()
    {
        return nav.hasPath;
    }
    #endregion

    #region 打中效果
    void BeHitChangeColor()
    {
        if (maxValue == 0)
        {
            maxValue = 10;
            myRender.material.SetColor("_EmissionColor", new Color(255, 0, 0, maxValue));
            myRender.material.EnableKeyword("_EMISSION");
            StartCoroutine(OriginalColor());
        }
        else
        {
            maxValue = 10;
            myRender.material.SetColor("_EmissionColor", new Color(255, 0, 0, maxValue));
        }
    }

    IEnumerator OriginalColor()
    {
        while (maxValue > 0)
        {
            maxValue -= Time.deltaTime * 70;
            myRender.material.SetColor("_EmissionColor", new Color(255, 0, 0, maxValue));
            if (maxValue <= 0)
            {
                maxValue = 0;
                myRender.material.DisableKeyword("_EMISSION");
                yield break;
            }
            yield return null;
        }
    }
    #endregion

    #region 受到傷害
    [PunRPC]
    public void takeDamage(float _damage, Vector3 _dir, bool ifHit)
    {
        if (deadManager.noDamage)
        {
            Debug.Log("產生無敵時被攻擊特效");
            return;
        }

        if (deadManager.checkDead)
            return;

        float tureDamage = CalculatorDamage(_damage);

        if (playerData.Hp_original > 0)
        {
            playerData.Hp_original -= tureDamage;
            //打擊音效
            //AudioScript.PlayAppointAudio(myAudio, 8);            
            BeHitChangeColor();
            ani.SetBool(AniControll.aniHashValue[8], true);
            openPopupObject(tureDamage);
            if (playerData.Hp_original <= 0)
            {
                deadManager.ifDead(true);
                ani.SetBool(AniControll.aniHashValue[15], true);
                Death();
            }
            if (ifHit && !deadManager.checkDead && !deadManager.notFeedBack && !NowCC)
            {
                CancelNowSkill();
                ani.SetTrigger(AniControll.aniHashValue[14]);
                beHit(_dir);
            }
        }
    }
    #endregion

    #region 顯示與計算傷害
    void openPopupObject(float _damage)
    {
        FloatTextCon.CreateFloatingText(_damage, myCachedTransform);
        UI_HpBar.fillAmount = playerData.Hp_original / playerData.Hp_Max;
        if (photonView.isMine)
            leftTopHpBar.fillAmount = playerData.Hp_original / playerData.Hp_Max;
    }
    
    private float CalculatorDamage(float _damage)
    {
        return _damage;
    }
    #endregion

    #region 死亡
    public void Death()
    {
        stopAnything_Switch(true);
        CharaCollider.enabled = false;
        AniControll.Die();
        if (photonView.isMine)
        {
            Invoke("Return_ObjPool", 3f);
            CancelNowSkill();
            cancelSkill.Invoke();
            CameraEffect.instance.nowDie(true);
            Creatplayer.instance.player_ReBorn(playerData.ReBorn_CountDown);
        }
    }

    void Return_ObjPool()
    {
        Net.RPC("SetActiveF", PhotonTargets.All);
    }
    #endregion
}