using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ChainboundRaid
{
    // Presentation only: clip time can never move the authoritative character.
    public sealed class RaidAnimationDriver : IDisposable
    {
        PlayableGraph graph;
        AnimationMixerPlayable mixer, locomotion;
        AnimationClipPlayable idle, walk, sprint, current, previous;
        AnimationClip selected;
        RaidCharacterPresentation profile;
        float transition;
        uint attackInstance;
        bool initialized;
        public RaidAnimationDriver(Animator animator, RaidCharacterPresentation settings)
        {
            profile = settings;
            animator.applyRootMotion = false;
            graph = PlayableGraph.Create("Raid Character Presentation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            locomotion = AnimationMixerPlayable.Create(graph, 3);
            idle = Create(settings.idle); walk = Create(settings.walk != null ? settings.walk : settings.idle);
            sprint = Create(settings.sprint != null ? settings.sprint : settings.walk != null ? settings.walk : settings.idle);
            Connect(idle, locomotion, 0); Connect(walk, locomotion, 1); Connect(sprint, locomotion, 2);
            graph.Connect(locomotion, 0, mixer, 0); mixer.SetInputWeight(0, 1f);
            var output = AnimationPlayableOutput.Create(graph, "Character", animator);
            output.SetSourcePlayable(mixer); graph.Play();
        }
        AnimationClipPlayable Create(AnimationClip clip) => clip != null ? AnimationClipPlayable.Create(graph, clip) : default;
        void Connect(AnimationClipPlayable clip, AnimationMixerPlayable destination, int input)
        { if (clip.IsValid()) graph.Connect(clip, 0, destination, input); }
        void DestroyClip(ref AnimationClipPlayable clip)
        { if (clip.IsValid()) graph.DestroyPlayable(clip); clip = default; }
        public void Evaluate(float speed, AnimationClip clip, uint instance, float normalizedAttack, bool timedAttack, float dt)
        {
            if (!graph.IsValid()) return;
            float walkSpeed = Mathf.Max(.1f, profile.walkReferenceSpeed);
            float runSpeed = Mathf.Max(walkSpeed + .1f, profile.sprintReferenceSpeed);
            float moving = Mathf.Clamp01(speed / walkSpeed);
            float running = Mathf.InverseLerp(walkSpeed, runSpeed, speed);
            locomotion.SetInputWeight(0, 1f - moving);
            locomotion.SetInputWeight(1, moving * (1f - running));
            locomotion.SetInputWeight(2, moving * running);
            if (walk.IsValid()) walk.SetSpeed(Mathf.Clamp(speed / walkSpeed, .25f, 1.5f));
            if (sprint.IsValid()) sprint.SetSpeed(Mathf.Clamp(speed / runSpeed, .25f, 1.5f));
            if (!initialized || clip != selected || (timedAttack && instance != attackInstance))
            {
                initialized = true;
                // Retain the dominant outgoing pose even if a transition is interrupted.
                if (mixer.GetInputWeight(1) >= mixer.GetInputWeight(2))
                {
                    if (previous.IsValid()) { mixer.DisconnectInput(2); DestroyClip(ref previous); }
                    if (current.IsValid())
                    { mixer.DisconnectInput(1); previous = current; current = default; Connect(previous, mixer, 2); }
                }
                else if (current.IsValid()) { mixer.DisconnectInput(1); DestroyClip(ref current); }
                selected = clip; attackInstance = instance; transition = 0f;
                current = Create(clip); Connect(current, mixer, 1);
            }
            transition = Mathf.Min(1f, transition + dt / Mathf.Max(.01f, profile.transitionSeconds));
            float incoming = Mathf.SmoothStep(0f, 1f, transition);
            float oldWeight = previous.IsValid() ? 1f - incoming : 0f;
            float newWeight = current.IsValid() ? incoming : 0f;
            mixer.SetInputWeight(0, 1f - oldWeight - newWeight);
            mixer.SetInputWeight(1, newWeight); mixer.SetInputWeight(2, oldWeight);
            if (current.IsValid())
            {
                current.SetSpeed(timedAttack ? 0f : 1f);
                if (timedAttack) current.SetTime(normalizedAttack * Mathf.Max(.001f, selected.length));
            }
            graph.Evaluate(dt);
        }
        public void Dispose() { if (graph.IsValid()) graph.Destroy(); }
    }
}
