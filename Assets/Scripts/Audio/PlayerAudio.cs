using Player;
using PurrNet;
using UnityEngine;

namespace Audio
{
    public class PlayerAudio : MonoBehaviour
    {
        public AudioSource playerAudioSource;
        public float audioSourceVolume = 0.25f;
        public float jumpAudioVolume = 0.1f;

        public AudioClip jumpAudio;
        public AudioClip headbuttChargeAudio;
        public AudioClip headbuttLaunchAudio;
        public AudioClip headbuttHitBall;
        public AudioClip headbuttHitPlayer;
        public AudioClip headbuttHitShell;
        public AudioClip headbuttHitOther;
        public AudioClip wormDie;
        public AudioClip wormRespawn;
        
        private Player.Player player;
        
        void Start()
        {
            player = GetComponent<Player.Player>();
            
            player.OnWormMoveForwardStart += OnWormForwardStart;
            player.OnWormMoveForwardEnd += OnWormForwardEnd;
            player.OnWormJump += OnWormJump;
            player.OnWormHeadbutCharge += OnWormHeadbutCharge;
            player.OnWormHeadbutLaunch += OnWormHeadbutLaunch;
            player.OnWormHeadbutHitBall += OnWormHeadbutHitBall;
            player.OnWormHeadbutHitPlayer += OnWormHeadbutHitPlayer;
            player.OnWormHeadbutHitShell += OnWormHeadbutHitShell;
            player.OnWormHeadbutHitOther += OnWormHeadbutHitOther;
            player.OnWormDeath += OnWormDeath;
            
            GetComponent<PlayerSpawning>().OnWormRespawn += OnWormRespawn;
        }

        void OnDestroy()
        {
            player = GetComponent<Player.Player>();
            
            player.OnWormMoveForwardStart -= OnWormForwardStart;
            player.OnWormMoveForwardEnd -= OnWormForwardEnd;
            player.OnWormJump -= OnWormJump;
            player.OnWormHeadbutCharge -= OnWormHeadbutCharge;
            player.OnWormHeadbutLaunch -= OnWormHeadbutLaunch;
            player.OnWormHeadbutHitBall -= OnWormHeadbutHitBall;
            player.OnWormHeadbutHitPlayer -= OnWormHeadbutHitPlayer;
            player.OnWormHeadbutHitShell -= OnWormHeadbutHitShell;
            player.OnWormHeadbutHitOther -= OnWormHeadbutHitOther;
            
            player.OnWormDeath -= OnWormDeath;
            
            GetComponent<PlayerSpawning>().OnWormRespawn -= OnWormRespawn;
        }

        private void OnWormRespawn()
        {
            playerAudioSource.volume = audioSourceVolume;
            playerAudioSource.clip = wormRespawn;
            playerAudioSource.Play();
        }

        private void OnWormDeath()
        {
            playerAudioSource.volume = audioSourceVolume;
            playerAudioSource.clip = wormDie;
            playerAudioSource.Play();
        }

        private void OnWormHeadbutHitOther()
        {
            //stop whooshing headbut sound, play hitOther sound
            playerAudioSource.volume = audioSourceVolume;
            playerAudioSource.clip = headbuttHitOther;
            playerAudioSource.Play();
        }

        private void OnWormHeadbutHitShell()
        {
            //stop whooshing headbut sound, play hitshell sound
            playerAudioSource.volume = audioSourceVolume;
            playerAudioSource.clip = headbuttHitShell;
            playerAudioSource.Play();
        }

        private void OnWormHeadbutHitPlayer()
        {
            //stop whooshing headbut sound, play hitPlayer sound
            playerAudioSource.volume = audioSourceVolume;
            playerAudioSource.clip = headbuttHitPlayer;
            playerAudioSource.Play();
        }

        private void OnWormHeadbutHitBall(Vector3 vector3)
        {
            //stop whooshing headbut sound, play hitBall sound
            playerAudioSource.volume = audioSourceVolume;
            playerAudioSource.clip = headbuttHitBall;
            playerAudioSource.Play();
        }

        private void OnWormHeadbutLaunch()
        {
            //worm headbut launch sound effect
            playerAudioSource.volume = audioSourceVolume;
            playerAudioSource.clip = headbuttLaunchAudio;
            playerAudioSource.Play();
        }

        private void OnWormHeadbutCharge()
        {
            //worm headbut charge sound effect
            // playerAudioSource.volume = audioSourceVolume;
            // playerAudioSource.clip = headbuttChargeAudio;
            // playerAudioSource.Play();
        }

        private void OnWormForwardStart()
        {
            //worm forward movement sound effects, possibly using speed and amount of grounded segments for variation
        }
        
        private void OnWormForwardEnd()
        {
            //stop movement sound effects
        }
        
        private void OnWormJump()
        {
            // use isWormGroundedbysegments to determine jump sound effect

            playerAudioSource.volume = jumpAudioVolume;
            playerAudioSource.clip = jumpAudio;
            playerAudioSource.Play();
        }
    }
}
