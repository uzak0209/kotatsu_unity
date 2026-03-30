using UnityEngine;

public class RespawnEffect : MonoBehaviour{
    [SerializeField]
    ParticleSystem respawnParticleSystem;
    [SerializeField]
    AudioSource respawnAudioSource;
    [SerializeField]
    AudioClip respawnAudioClip;

    public void PlayEffect(){
        respawnParticleSystem.Play();
        respawnAudioSource.PlayOneShot(respawnAudioClip);
    }
}
