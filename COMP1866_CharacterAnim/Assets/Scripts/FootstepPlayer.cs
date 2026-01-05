using UnityEngine;

// Plays footstep SFX based on animator events and speed parameter.
public class FootstepPlayer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;

    [Header("Clips")]
    public AudioClip[] walkFootsteps;
    public AudioClip[] runFootsteps;

    [Header("Tuning")]
    [Range(0f, 1f)] public float volume = 0.6f;

    [SerializeField] private string speedParam = "Speed"; // animator Speed param
    [SerializeField] private float runThreshold = 0.6f; // above = running
    [SerializeField] private bool ignoreDuringTransitions = true;

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    // Called from animation event for walk footstep
    public void FootstepWalk()
    {
        if (!CanPlayFootstep()) return;

        float s = animator ? animator.GetFloat(speedParam) : 0f;
        if (s >= runThreshold) return; // skip if running

        PlayRandom(walkFootsteps);
    }

    // Called from animation event for run footstep
    public void FootstepRun()
    {
        if (!CanPlayFootstep()) return;

        float s = animator ? animator.GetFloat(speedParam) : 0f;
        if (s < runThreshold) return; // skip if walking

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
