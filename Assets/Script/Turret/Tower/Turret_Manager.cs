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
        [SerializeField] GameManager.whichObject DataName;
        protected TurretData.TowerDataBase turretData;
        protected TurretData.TowerDataBase originalTurretData;
        protected float nowCD = 0;
        [SerializeField]
        LayerMask currentMask;

        //正確目標
        protected Transform target;
        [Header("位置")]
        public Transform Pos_rotation;
        public Transform Pos_attack;
        [Header("UI部分")]
        public Image Fad_energyBar;
        protected isDead deadManager;

        protected PhotonView Net;
        private void Start()
        {
            deadManager = GetComponent<isDead>();
            Net = GetComponent<PhotonView>();
            formatData();
            if (photonView.isMine)
            {
                checkCurrentPlay();
            }
            else if (!photonView.isMine)
            {
                this.enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (deadManager.checkDead)  //死亡
                return;

            overHeat();

            if (turretData.Fad_overHeat) //過熱
                return;

            if (target == null)
                FindEnemy();
            else
            {
                DetectTarget();
                LockOnTarget();
            }

            if (nowCD > 0)
                nowCD -= Time.deltaTime;
        }

        #region 恢復初始數據
        protected void formatData()
        {
            originalTurretData = TurretData.instance.getTowerData(DataName);
            turretData = originalTurretData;
            Net.RPC("show_HPBar", PhotonTargets.All, 1.0f);
            Net.RPC("showHeatBar", PhotonTargets.All, 0.0f);
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

        #region 尋找敵人
        public void FindEnemy()
        {
            Collider[] AllEnemys = Physics.OverlapSphere(transform.position, turretData.Atk_Range + 1.5f, currentMask);

            foreach (var enemyObj in AllEnemys)
            {
                Vector3 maxDisGap = enemyObj.transform.position - transform.position;
                float maxDis = maxDisGap.sqrMagnitude;

                if (maxDis <= Mathf.Pow(turretData.Atk_Range, 2) && maxDis >= Mathf.Pow(turretData.Atk_MinRange, 2))
                {
                    if (!enemyObj.GetComponent<isDead>().checkDead)
                    {
                        if (enemyObj.gameObject.activeInHierarchy)
                            target = enemyObj.transform;
                    }
                }
            }
        }
        #endregion

        #region 偵測是否死亡與超出攻擊範圍
        void DetectTarget()
        {
            if (target != null)
            {
                if (target.GetComponent<isDead>().checkDead)
                {
                    target = null;
                }
                else
                {
                    Vector3 maxDisGap = target.transform.position - transform.position;
                    float distanceToEnemy = maxDisGap.sqrMagnitude;

                    if (distanceToEnemy > Mathf.Pow(turretData.Atk_Range, 2) || distanceToEnemy < Mathf.Pow(turretData.Atk_MinRange, 2))
                    {
                        target = null;
                    }
                }
            }
        }
        #endregion

        #region 朝向敵方目標
        void LockOnTarget()
        {
            if (target != null)
            {
                //砲台與敵人位置方向
                Vector3 dir = target.position - Pos_attack.position;
                //轉向dir
                Quaternion lookRotation = Quaternion.LookRotation(dir);
                Vector3 rotation = Quaternion.Lerp(Pos_rotation.rotation, lookRotation, Time.deltaTime * 10).eulerAngles;
                Pos_rotation.rotation = Quaternion.Euler(0/*rotation.x*/, rotation.y, 0f);
                float tmpAngle = Quaternion.Angle(Pos_rotation.rotation, lookRotation);

               // Debug.Log("角度" + tmpAngle);
                if (tmpAngle < 35)
                {
                    DecidedNowTurret();
                }
            }
        }
        #endregion

        #region 攻擊間隔
        void DecidedNowTurret()
        {
            if (target != null && !turretData.Fad_overHeat)
            {
                if (nowCD <= 0)
                {
                    Tower_shoot();
                    nowCD = turretData.Atk_Gap;
                }
            }
        }
        #endregion

        #region 攻擊函式_覆蓋區
        public virtual void Tower_shoot()
        {
            addHeat(1.0f);
            float _value = turretData.Fad_thermalEnergy / turretData.Fad_maxThermalEnergy;
            Net.RPC("showHeatBar", PhotonTargets.All, _value);
        }
        #endregion

        #region 熱能處理
        void overHeat()
        {
            if (!turretData.Fad_overHeat)
            {
                if (turretData.Fad_thermalEnergy > 0 && turretData.Fad_thermalEnergy < turretData.Fad_maxThermalEnergy)
                {
                    reduceHeat(1.0f);
        
                    float _value = turretData.Fad_thermalEnergy / turretData.Fad_maxThermalEnergy;
                    Net.RPC("showHeatBar", PhotonTargets.All, _value);
                }
            }
            else
            {
                reduceHeat(turretData.Over_downSpd);

                float _value = turretData.Fad_thermalEnergy / turretData.Fad_maxThermalEnergy;
                Net.RPC("showHeatBar", PhotonTargets.All, _value);
                
            }
        }

        #region 增加減少熱能
        //減少
        public void reduceHeat(float _speed)
        {
            float tmpValue = turretData.Fad_decreaseRate * Time.deltaTime * _speed;
            if (turretData.Fad_thermalEnergy - tmpValue <= 0)
            {
                Net.RPC("changeColor", PhotonTargets.All, 242, 235, 0);
                turretData.Fad_overHeat = false;
            }
            else
            {
                turretData.Fad_thermalEnergy -= tmpValue;
            }
        }
        //增加
        public void addHeat(float _speed)
        {
            float tmpValue = turretData.Fad_oneEnergy * _speed;
            if (turretData.Fad_thermalEnergy + tmpValue >= turretData.Fad_maxThermalEnergy)
            {
                turretData.Fad_thermalEnergy = turretData.Fad_maxThermalEnergy;
                Net.RPC("changeColor", PhotonTargets.All, 255, 142, 81);
                turretData.Fad_overHeat = true;
            }
            else
            {
                turretData.Fad_thermalEnergy += tmpValue;
            }
        }
        #endregion

        //熱量條傳遞
        [PunRPC]
        public void showHeatBar(float _value)
        {
            Fad_energyBar.fillAmount = _value;
        }

        //改顏色
        [PunRPC]
        public void changeColor(int _R, int _G, int _B)
        {
            Fad_energyBar.color = new Color(_R, _G, _B);
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
            if (photonView.isMine)
            {
                if (turretData.UI_Hp > 0)
                    turretData.UI_Hp -= tureDamage;
                
                if (turretData.UI_Hp <= 0)
                    Net.RPC("NowDeath", PhotonTargets.All);              
            }
            openPopupObject(tureDamage);
        }
        #endregion

        #region 傷害顯示
        void openPopupObject(float _damage)
        {
            FloatingTextController.instance.CreateFloatingText(_damage.ToString("0.0"), this.transform);
            if (!photonView.isMine)
                return;
            float _value = turretData.UI_Hp / turretData.UI_maxHp;
            Net.RPC("show_HPBar", PhotonTargets.All, _value);
        }
        #endregion

        //同步血量
        [PunRPC]
        public void show_HPBar(float _value)
        {
            healthBar.fillAmount = _value;
        }

        //同步死亡
        [PunRPC]
        public void NowDeath()
        {
            deadManager.ifDead(true);
            StartCoroutine(Death());
        }

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
            formatData();
            returnBulletPool();
        }
        #endregion

        #region 返回物件池
        protected void returnBulletPool()
        {
            ObjectPooler.instance.Repool(DataName, this.gameObject);
        }
        #endregion

        public void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, turretData.Atk_Range);
            if (target != null)
            {
                Gizmos.DrawLine(Pos_attack.position, target.position);
            }
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, turretData.Atk_MinRange);
        }
    }
}