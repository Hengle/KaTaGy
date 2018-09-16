﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
namespace AtkTower
{
    [RequireComponent(typeof(isDead))]
    public class Turret_Manager : Photon.MonoBehaviour
    {
        //數據
        public GameManager.whichObject DataName;
        protected TurretData.TowerDataBase turretData;
        protected TurretData.TowerDataBase originalTurretData;
        protected float nowCD = 0;
        [SerializeField] LayerMask currentMask;
        public int GridNumber;
        public Electricity power;

        //正確目標
        protected Transform target;
        [Header("位置")]
        [SerializeField] Transform Pos_rotation;
        public Transform Pos_attack;

        [Header("UI部分")]
        public Image Fad_energyBar;

        protected isDead deadManager;
        protected FloatingTextController floatingText;
        protected PhotonView Net;

        private SceneObjManager sceneObjManager;
        private SceneObjManager SceneManager { get { if (sceneObjManager == null) sceneObjManager = SceneObjManager.Instance; return sceneObjManager; } }

        private void Awake()
        {
            Net = GetComponent<PhotonView>();
            floatingText = FloatingTextController.instance;            
        }

        private void Start()
        {           
            if (photonView.isMine)
            {
                checkCurrentPlay();
            }
            else
            {
                this.enabled = false;
            }
        }

        private void OnEnable()
        {
            formatData();
        }

        private void Update()
        {
            if (turretData.Fad_thermalEnergy > 0)
            {
                overHeat();
            }

            if (deadManager.checkDead || turretData.Fad_overHeat || power == null || power.resource_Electricity < 0)  //死亡、沒電、過熱
            {
                return;
            }

            if (target == null)
            {
                FindEnemy();
            }

            if (target != null)
            {
                DetectTarget();
                LockOnTarget();
            }
            nowCD -= Time.deltaTime;
        }

        #region 恢復初始數據
        protected void formatData()
        {
            if (deadManager == null)
            {
                deadManager = GetComponent<isDead>();
            }
            else
            {
                if (photonView.isMine)
                {
                    originalTurretData = TurretData.instance.getTowerData(DataName);
                    SceneManager.AddMy_TowerList(gameObject);
                }
                else
                {
                    originalTurretData = TurretData.instance.getEnemyTowerData(DataName);
                    SceneManager.AddEnemy_TowerList(gameObject);
                }

                deadManager.ifDead(false);
                turretData = originalTurretData;
                nowCD = turretData.Atk_Gap;
                turretData.UI_Hp = turretData.UI_maxHp;
                turretData.Fad_thermalEnergy = 0;
            }

            healthBar.fillAmount = 1;
            Fad_energyBar.fillAmount = 0.0f;
        }
        #endregion

        #region 目前為玩家幾
        public void checkCurrentPlay()
        {
            if (GameManager.instance.getMyPlayer() == GameManager.MyNowPlayer.player_1)
            {
                Net.RPC("changeLayer", PhotonTargets.All, 30);
                currentMask = GameManager.instance.getPlayer1_Mask;
            }
            else if (GameManager.instance.getMyPlayer() == GameManager.MyNowPlayer.player_2)
            {
                Net.RPC("changeLayer", PhotonTargets.All, 31);
                currentMask = GameManager.instance.getPlayer2_Mask;
            }
        }
        #endregion

        GameObject tmpTarget;
        #region 尋找敵人
        public void FindEnemy()
        {
            tmpTarget = null;
            tmpTarget = SceneManager.CalculationDis(gameObject, turretData.Atk_Range, turretData.Atk_MinRange);

            if (tmpTarget != null)
            {
                target = tmpTarget.transform;
            }
        }
        #endregion

        #region 偵測是否死亡與超出攻擊範圍
        void DetectTarget()
        {
            if (target.GetComponent<isDead>().checkDead)
            {
                target = null;
            }
            else
            {
                float distanceToEnemy = Vector3.Distance(target.transform.position, transform.position);

                if (distanceToEnemy > turretData.Atk_Range || distanceToEnemy < turretData.Atk_MinRange)
                {
                    target = null;
                }
            }
        }
        #endregion

        #region 朝向敵方目標
        void LockOnTarget()
        {
            if (target == null)
            {
                return;
            }

            //轉向
            Quaternion lookRotation = Quaternion.LookRotation(target.position - Pos_attack.position);
            Vector3 rotation = Quaternion.Lerp(Pos_rotation.rotation, lookRotation, Time.deltaTime * 10).eulerAngles;
            Pos_rotation.rotation = Quaternion.Euler(0/*rotation.x*/, rotation.y, 0f);
            float tmpAngle = Quaternion.Angle(Pos_rotation.rotation, lookRotation);

            // Debug.Log("角度" + tmpAngle);
            if (tmpAngle < 30)
            {
                DecidedNowTurret();
            }
        }
        #endregion

        #region 攻擊間隔
        void DecidedNowTurret()
        {
            if (target != null && !turretData.Fad_overHeat)
            {
                if (nowCD <= 0 && photonView.isMine)
                {
                    Tower_shoot();
                    nowCD = turretData.Atk_Gap;
                }
            }
        }
        #endregion

        #region 攻擊函式_覆蓋區
        protected virtual void Tower_shoot()
        {
            addHeat(1.0f);
            float _value = turretData.Fad_thermalEnergy / turretData.Fad_maxThermalEnergy;
            Fad_energyBar.fillAmount = _value;

            GameObject bulletObj = ObjectPooler.instance.getPoolObject(turretData.bullet_Name, Pos_attack.position, Pos_attack.rotation);
            BulletManager bullet = bulletObj.GetComponent<BulletManager>();
            bullet.getTarget(target);
        }
        #endregion

        #region 熱能處理
        void overHeat()
        {
            reduceHeat((!turretData.Fad_overHeat) ? 1.0f : turretData.Over_downSpd);

            float _value = turretData.Fad_thermalEnergy / turretData.Fad_maxThermalEnergy;
            Fad_energyBar.fillAmount = _value;
        }
        #endregion

        [PunRPC]
        public void OverheatChange(int _r, int _b, int _g)
        {
            Fad_energyBar.color = new Color(_r, _b, _g);
        }

        #region 增加減少熱能
        //減少
        public void reduceHeat(float _speed)
        {
            float tmpValue = turretData.Fad_decreaseRate * Time.deltaTime * _speed;

            turretData.Fad_thermalEnergy -= tmpValue;

            if (turretData.Fad_thermalEnergy <= 0)
            {
                turretData.Fad_thermalEnergy = 0;
                turretData.Fad_overHeat = false;
                Net.RPC("OverheatChange", PhotonTargets.All, 242, 235, 0);
                //Fad_energyBar.color = new Color(242, 235, 0);
            }
        }
        //增加
        public void addHeat(float _speed)
        {
            print("增加熱量");
            float tmpValue = turretData.Fad_oneEnergy * _speed;

            turretData.Fad_thermalEnergy += tmpValue;

            if (turretData.Fad_thermalEnergy >= turretData.Fad_maxThermalEnergy)
            {
                turretData.Fad_thermalEnergy = turretData.Fad_maxThermalEnergy;
                turretData.Fad_overHeat = true;
                Net.RPC("OverheatChange", PhotonTargets.All, 255, 142, 81);
                //Fad_energyBar.color = new Color(255, 142, 81);
            }
        }
        #endregion

        #region 傷害
        public Image healthBar;
        [PunRPC]
        public void takeDamage(float _damage)
        {
            if (deadManager.checkDead)
                return;

            float tureDamage = CalculatorDamage(_damage);
            turretData.UI_Hp -= tureDamage;
            openPopupObject(tureDamage);

            if (turretData.UI_Hp <= 0)
            {
                if (photonView.isMine)
                {
                    SceneManager.RemoveMy_TowerList(gameObject);
                    BuildManager.instance.obtaniElectricity(this);
                }
                else
                {
                    SceneManager.RemoveEnemy_TowerList(gameObject);
                }

                deadManager.ifDead(true);
                StartCoroutine(Death());
            }
        }
        #endregion

        #region 傷害顯示
        void openPopupObject(float _damage)
        {
            floatingText.CreateFloatingText(_damage, this.transform);
            healthBar.fillAmount = turretData.UI_Hp / turretData.UI_maxHp;
        }
        #endregion

        #region 計算傷害
        protected virtual float CalculatorDamage(float _damage)
        {
            return 0.0f;
        }
        #endregion

        #region 死亡
        protected virtual IEnumerator Death()
        {
            yield return new WaitForSeconds(1.5f);
            returnBulletPool();
        }
        #endregion

        #region 返回物件池
        protected void returnBulletPool()
        {
            if (photonView.isMine)
                ObjectPooler.instance.Repool(DataName, this.gameObject);
        }
        #endregion

        #region 過熱同步
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.isWriting)
            {
                //stream.SendNext(turretData.UI_Hp);
                //stream.SendNext(turretData.UI_maxHp);
                stream.SendNext(Fad_energyBar.fillAmount);
                /*stream.SendNext(Fad_energyBar.color.r);
                stream.SendNext(Fad_energyBar.color.g);
                stream.SendNext(Fad_energyBar.color.b);*/
            }
            else
            {
                //turretData.UI_Hp = (float)stream.ReceiveNext();
                //turretData.UI_maxHp = (float)stream.ReceiveNext();
                Fad_energyBar.fillAmount = (float)stream.ReceiveNext();
                /*float _r = (float)stream.ReceiveNext();
                float _g = (float)stream.ReceiveNext();
                float _b = (float)stream.ReceiveNext();*/

                /*if (Fad_energyBar.color.r != _r)
                {
                    Fad_energyBar.color = new Color(_r, _g, _b);
                }*/

                /*if (turretData.UI_maxHp != originalTurretData.UI_maxHp)
                {
                    originalTurretData.UI_maxHp = turretData.UI_maxHp;
                    print("升級血量變動");
                    healthBar.fillAmount = turretData.UI_Hp / turretData.UI_maxHp;
                }*/
            }
        }
        #endregion

        /*  public void OnDrawGizmos()
          {
              Gizmos.color = Color.red;
              Gizmos.DrawWireSphere(transform.position, turretData.Atk_Range);
              if (target != null)
              {
                  Gizmos.DrawLine(Pos_attack.position, target.position);
              }
              Gizmos.color = Color.blue;
              Gizmos.DrawWireSphere(transform.position, turretData.Atk_MinRange);
          }*/
    }
}
