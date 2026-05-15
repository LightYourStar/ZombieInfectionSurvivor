using UnityEngine;

namespace Game.Gameplay.Feedback
{
    /// <summary>
    /// 轻量音效反馈管理器。
    /// 提供统一的音效播放接口，当前可以没有真实 AudioClip，安全跳过不报错。
    /// 后续替换音效资源时只需在 Inspector 中赋值即可。
    /// 场景中挂载一个即可，通过 Instance 单例访问。
    /// </summary>
    public class GameAudioFeedback : MonoBehaviour
    {
        public static GameAudioFeedback Instance { get; private set; }

        [Header("音效资源（可选，未赋值时静默跳过）")]
        [SerializeField] private AudioClip m_infectionClip;
        [SerializeField] private AudioClip m_burstClip;
        [SerializeField] private AudioClip m_comboMilestoneClip;
        [SerializeField] private AudioClip m_upgradeSelectedClip;
        [SerializeField] private AudioClip m_finalFrenzyStartClip;
        [SerializeField] private AudioClip m_resultClip;

        [Header("音量")]
        [SerializeField, Range(0f, 1f)] private float m_sfxVolume = 0.7f;

        private AudioSource m_audioSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            m_audioSource = GetComponent<AudioSource>();
            if (m_audioSource == null)
            {
                m_audioSource = gameObject.AddComponent<AudioSource>();
            }
            m_audioSource.playOnAwake = false;
        }

        public void PlayInfection() => TryPlay(m_infectionClip);
        public void PlayBurst() => TryPlay(m_burstClip);
        public void PlayComboMilestone() => TryPlay(m_comboMilestoneClip);
        public void PlayUpgradeSelected() => TryPlay(m_upgradeSelectedClip);
        public void PlayFinalFrenzyStart() => TryPlay(m_finalFrenzyStartClip);
        public void PlayResult() => TryPlay(m_resultClip);

        private void TryPlay(AudioClip clip)
        {
            if (clip == null || m_audioSource == null) return;
            m_audioSource.PlayOneShot(clip, m_sfxVolume);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
