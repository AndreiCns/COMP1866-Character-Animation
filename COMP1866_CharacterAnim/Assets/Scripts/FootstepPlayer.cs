using UnityEngine;

public class FootstepPlayer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;

    [Header("Clips")]
    public AudioClip[] walkFootsteps;
    public AudioClip[] runFootsteps;

    [Header("Tuning")]
    [Range(0f, 1f)] public float volume = 0.6f;

    // This should match whatever your movement uses (often animator "Speed" 0..1)
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private float runThreshold = 0.6f; // above this = running
    [SerializeField] private bool ignoreDuringTransitions = true;

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    public void FootstepWalk()
    {
        if (!CanPlayFootstep()) return;

        float s = animator ? animator.GetFloat(speedParam) : 0f;
        if (s >= runThreshold) return; // if we're running, ignore walk events

        PlayRandom(walkFootsteps);
    }

    public void FootstepRun()
    {
        if (!CanPlayFootstep()) return;

        float s = animator ? animator.GetFloat(speedParam) : 0f;
        if (s < runThreshold) return; // if we're walking, ignore run events

        PlayRandom(runFootsteps);
    }

    bool CanPlayFootstep()
    {
        if (animator == null) return true;
        if (ignoreDuringTransitions && animator.IsInTransition(0)) return false;
        return true;
    }

    void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        JukeboxAudio.PlaySFX(clip, volume);
    }
}
