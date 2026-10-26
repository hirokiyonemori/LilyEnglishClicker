using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{

    [Header("Audio")]
    public AudioSource Sound;

    [Header("Audio Effects")]
    public AudioClip Coin; // 既存のコインSE
    public AudioClip CoinBurst; // コイン飛び散りSE（ジャララッ！）
    public AudioClip CoinCollect; // コイン回収SE（ピコーン！）

    // Method of playing effect, accepts any effect from cached
    public void PlaySound(AudioClip sound)
    {
        Sound.clip = sound;
        Sound.Play();
    }
}
