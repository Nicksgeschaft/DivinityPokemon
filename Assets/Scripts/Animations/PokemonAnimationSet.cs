using System;
using System.Collections.Generic;
using UnityEngine;

public enum PokemonAnimId
{
    None = 0,

    Walk,
    Attack,
    Kick,
    Shoot,
    Strike,
    Sleep,
    Hurt,
    Idle,
    Swing,
    Double,
    Hop,
    Charge,
    Rotate,
    EventSleep,
    Wake,
    Eat,
    Tumble,
    Pose,
    Pull,
    Pain,
    Float,
    DeepBreath,
    Nod,
    Sit,
    LookUp,
    Sink,
    Trip,
    Laying,
    LeapForth,
    Head,
    Cringe,
    LostBalance,
    TumbleBack,
    Faint,
    HitGround
}

[Serializable]
public class PokemonAnimationDefinition
{
    public PokemonAnimId id;
    public string name;

    public int index;
    public int frameWidth;
    public int frameHeight;

    public int rushFrame = -1;
    public int hitFrame = -1;
    public int returnFrame = -1;

    public bool loop;
    public string copyOf;

    public Sprite[] bodyFrames;
    public Sprite[] shadowFrames;

    public List<int> durations = new();

    public int FrameCount => durations != null ? durations.Count : 0;
}

[CreateAssetMenu(menuName = "Pokemon/Animation Set", fileName = "PokemonAnimationSet")]
public class PokemonAnimationSet : ScriptableObject
{
    public int shadowSize = 1;
    public List<PokemonAnimationDefinition> animations = new();

    private Dictionary<PokemonAnimId, PokemonAnimationDefinition> _byId;
    private Dictionary<string, PokemonAnimationDefinition> _byName;

    public void RebuildCache()
    {
        _byId = new Dictionary<PokemonAnimId, PokemonAnimationDefinition>();
        _byName = new Dictionary<string, PokemonAnimationDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var anim in animations)
        {
            if (anim == null)
                continue;

            _byId[anim.id] = anim;

            if (!string.IsNullOrWhiteSpace(anim.name))
                _byName[anim.name] = anim;
        }
    }

    public PokemonAnimationDefinition Get(PokemonAnimId id)
    {
        if (_byId == null)
            RebuildCache();

        _byId.TryGetValue(id, out var anim);
        return ResolveCopy(anim);
    }

    public PokemonAnimationDefinition Get(string name)
    {
        if (_byName == null)
            RebuildCache();

        _byName.TryGetValue(name, out var anim);
        return ResolveCopy(anim);
    }

    private PokemonAnimationDefinition ResolveCopy(PokemonAnimationDefinition anim)
    {
        if (anim == null)
            return null;

        if (string.IsNullOrWhiteSpace(anim.copyOf))
            return anim;

        if (_byName == null)
            RebuildCache();

        if (_byName.TryGetValue(anim.copyOf, out var target) && target != anim)
            return target;

        return anim;
    }
}