﻿using UnityEngine;

public class Queen_Ani : PlayerAni
{
    #region 設定攻擊Collider
    protected override void SetCheckBox()
    {
        checkEnemyBox[0] = new Vector3(3.5f, 1f, 1.5f);
        checkEnemyBox[1] = new Vector3(3.5f, 1f, 3.5f);
    }
    #endregion

    #region 按下判斷
    public override void TypeCombo(Vector3 atkDir)
    {
        if (canClick)
        {
            if (comboIndex == 0 && (anim.GetCurrentAnimatorStateInfo(0).fullPathHash == aniHashValue[24] || anim.GetCurrentAnimatorStateInfo(0).fullPathHash == aniHashValue[25] || 
                anim.GetCurrentAnimatorStateInfo(0).fullPathHash == aniHashValue[17]))
            {
                canClick = false;
                comboFirst(1, atkDir);
            }
            if (anim.GetCurrentAnimatorStateInfo(0).fullPathHash == aniHashValue[20] && comboIndex == 1)
            {
                canClick = false;
                Nextcombo(2);
            }
            if (anim.GetCurrentAnimatorStateInfo(0).fullPathHash == aniHashValue[21] && comboIndex == 2)
            {
                canClick = false;
                Nextcombo(3);
            }
            if (anim.GetCurrentAnimatorStateInfo(0).fullPathHash == aniHashValue[22] && comboIndex == 3)
            {
                canClick = false;
                Nextcombo(4);
            }
        }
    }
    #endregion

    #region Combo動畫播放間判定
    public override void comboCheck(int _n)
    {
        switch (_n)
        {
            //預測點
            case (0):
                NowComboAudio();
                if (photonView.isMine)
                {
                    canClick = true;
                    anim.SetBool(aniHashValue[6], false);
                    player.lockDodge = false;
                }
                break;
            //結束點
            case (2):
                if (nextComboBool)
                {
                    goNextCombo();
                    SwitchAtkRange(8);
                    player.lockDodge = false;
                }
                else if (!anim.GetBool(aniHashValue[6]))
                {
                    SwitchAtkRange(8);
                    player.lockDodge = false;                    
                    GoBackIdle_canMove();
                    anim.CrossFade(aniHashValue[24], .25f);
                }
                break;
            //前搖點
            case (3):
                if (photonView.isMine)
                {
                    //鎖閃避
                    player.lockDodge = true;
                    redressOpen = true;
                    brfore_shaking = true;
                }
                break;
            //後搖點
            case (4):
                if (photonView.isMine)
                {
                    //解閃避
                    player.lockDodge = false;
                    redressOpen = false;
                    after_shaking = true;
                    if (!canClick && nextComboBool)
                    {
                        goNextCombo();
                        SwitchAtkRange(8);
                    }
                }
                break;
            default:
                break;
        }
    }
    #endregion

    #region 傷害判定
    public override void DetectAtkRanage()
    {
        base.DetectAtkRanage();

        //鐮刀本身
       if (startDetect_1)
        {
            ProduceCheckBox(weapon_Detect, checkEnemyBox[0]);
        }
       //轉刀
         if (startDetect_2)
        {
            ProduceCheckBox(weapon_Detect, checkEnemyBox[1]);
        }
    }

    void ProduceCheckBox(Transform _pos, Vector3 _size)
    {
        checkBox = Physics.OverlapBox(_pos.position, _size, _pos.rotation, canAtkMask);
        if (photonView.isMine && checkBox.Length != 0)
            GetCurrentTarget();
    }
    #endregion

    #region 給予正確目標傷害
    protected override void GetCurrentTarget()
    {
        arrayAmount = checkBox.Length;
        for (int i = 0; i < arrayAmount; i++)
        {
            if (alreadyDamage.Contains(checkBox[i].gameObject))
                continue;

            checkTag = checkBox[i].GetComponent<isDead>();
            if (!checkTag.checkDead)
            {
                Net = checkBox[i].GetComponent<PhotonView>();
                switch (checkTag.myAttributes)
                {
                    case (GameManager.NowTarget.Soldier):
                        if (startDetect_1)
                        {
                            Net.RPC("takeDamage", PhotonTargets.All, player.Net.viewID, 3.0f);
                        }
                        else
                            Net.RPC("takeDamage", PhotonTargets.All, player.Net.viewID, 4.0f);
                        break;
                    case (GameManager.NowTarget.Tower):
                        Net.RPC("takeDamage", PhotonTargets.All, 10.0f);
                        break;
                    case (GameManager.NowTarget.Player):
                        if (startDetect_1)
                            Net.RPC("takeDamage", PhotonTargets.All, 3.0f, currentAtkDir.normalized, true);
                        else
                            Net.RPC("takeDamage", PhotonTargets.All, 5.5f, currentAtkDir.normalized, true);                        
                        break;
                    case (GameManager.NowTarget.Ore):
                        Net.RPC("takeDamage", PhotonTargets.All, false, player.Net.viewID);
                        break;
                    case (GameManager.NowTarget.Core):
                        Debug.Log("還沒寫");
                        break;
                    default:
                        Debug.Log("錯誤");
                        break;
                }
                alreadyDamage.Add(checkBox[i].gameObject);
            }
        }
    }
    #endregion

    //粒子特效位子跟旋轉
    //combo1
    Vector3 PS1_Pos = new Vector3(0.07f, 1.58f, .12f);
    Vector3 PS1_Rot = new Vector3(285.54f, -65.7f, 271.8f);
    //combo2
    Vector3 PS2_Pos = new Vector3(.74f, 2.57f, .89f);
    Vector3 PS2_Rot = new Vector3(80.76f, -136.5f, -148.6f);
    #region 目前傷害判定區及刀光特效
    public override void SwitchAtkRange(int _n)
    {
        switch (_n)
        {
            case (0):
                startDetect_1 = true;
                break;
            case (1):
                startDetect_2 = true;
                break;
            //刀光1
            case (2):
                if (comboIndex == 1 || comboIndex == 2)
                {
                    swordLight[0].transform.localPosition = PS1_Pos;
                    swordLight[0].transform.localEulerAngles = PS1_Rot;
                    swordLight[0].Play();
                }
                break;
            //刀光2
            case (3):
                if (comboIndex == 2 || comboIndex == 3)
                {
                    swordLight[0].transform.localPosition = PS2_Pos;
                    swordLight[0].transform.localEulerAngles = PS2_Rot;
                    swordLight[0].Play();
                }
                break;
            //刀光3
            case (4):
                if (comboIndex == 3 || comboIndex == 4)
                {
                    swordLight[1].Play();
                }
                break;
            //刀光4
            case (5):
                if (comboIndex == 4)
                {
                    swordLight[3].transform.forward = transform.forward;
                    swordLight[2].Play();
                    swordLight[3].Play();
                }
                break;            
            default://8
                startDetect_1 = false;
                startDetect_2 = false;
                for (int i = 0; i < 4; i++)
                {
                    swordLight[i].Stop();
                }
                alreadyDamage.Clear();
                break;
        }
    }
    #endregion

    void NowComboAudio()
    {
        //刀光1,2
        if (comboIndex == 1 || comboIndex == 2)
        {
            player.AudioScript.PlayAppointAudio(comboAudio, 5);
        }
        //刀光3
        if (comboIndex == 3)
        {
            player.AudioScript.PlayAppointAudio(comboAudio, 6);
        }
        //刀光4
        if (comboIndex == 4)
        {
            player.AudioScript.PlayAppointAudio(comboAudio, 7);
        }
    }
}