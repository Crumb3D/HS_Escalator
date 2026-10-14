using System;
using UnityEngine;

// Looped drive rumble under a running belt. One 3D speaker sits at the nearest
// point on the mechanism so standing on it stays loud and walking away fades it.
public static class HSEscalatorSounds
{
    const float TargetVol = 0.2f;
    const float FadePerSec = 2.4f;
    const float MinDistance = 5f;
    const float MaxDistance = 22f;

    static AudioClip drive;

    public static void Tick(HSEscalatorController host, HSEscalatorConfigData d, ref AudioSource src)
    {
        if (GameManager.IsDedicatedServer || host == null) return;
        bool on = d != null && d.Captured && d.HasDeck && d.Running;
        if (!on)
        {
            if (src == null) return;
            src.volume = Mathf.MoveTowards(src.volume, 0f, Time.deltaTime * FadePerSec);
            if (src.volume <= 0.001f)
            {
                src.Stop();
                return;
            }
            Place(src, d);
            return;
        }

        if (src == null) src = Make(host);
        if (src.clip == null) src.clip = Clip();
        if (!src.isPlaying) src.Play();
        src.pitch = Pitch(d);
        src.volume = Mathf.MoveTowards(src.volume, TargetVol, Time.deltaTime * FadePerSec);
        Place(src, d);
    }

    public static void Destroy(ref AudioSource src)
    {
        if (src == null) return;
        var go = src.gameObject;
        src = null;
        if (go != null) UnityEngine.Object.Destroy(go);
    }

    static AudioSource Make(HSEscalatorController host)
    {
        var go = new GameObject("HSEscalatorDriveSound");
        go.transform.SetParent(host.transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = Clip();
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.spatialize = false;
        src.dopplerLevel = 0f;
        src.spread = 50f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = MinDistance;
        src.maxDistance = MaxDistance;
        src.priority = 160;
        src.volume = 0f;
        var lp = go.AddComponent<AudioLowPassFilter>();
        lp.cutoffFrequency = 1350f;
        return src;
    }

    static void Place(AudioSource src, HSEscalatorConfigData d)
    {
        if (src == null) return;
        Vector3 world;
        if (!TryUnder(d != null ? d.ToPath() : null, out world))
            return;
        src.transform.position = world - Origin.position;
        src.spatialBlend = 1f;
    }

    static bool TryUnder(HSEscalatorPath path, out Vector3 world)
    {
        world = Vector3.zero;
        if (path == null || path.Length < 1 || path.Width < 1) return false;
        float col = (path.Length - 1) * 0.5f;
        int lane = path.Width / 2;
        var gm = GameManager.Instance;
        var w = gm != null ? gm.World : null;
        var player = w != null ? w.GetPrimaryPlayer() : null;
        if (player != null)
        {
            float pc;
            int pl;
            if (path.TryWorldCol(player.position.x, player.position.z, out pc, out pl))
            {
                col = Mathf.Clamp(pc, 0f, path.Length - 1);
                lane = pl;
            }
            else if (path.RunAxis == 0)
            {
                col = Mathf.Clamp((player.position.x - path.OriginX) / (path.RunSign == 0 ? 1 : path.RunSign), 0f, path.Length - 1);
                lane = Mathf.Clamp(Mathf.FloorToInt(player.position.z - path.LaneMinZ), 0, path.Width - 1);
            }
            else
            {
                col = Mathf.Clamp((player.position.z - path.OriginZ) / (path.RunSign == 0 ? 1 : path.RunSign), 0f, path.Length - 1);
                lane = Mathf.Clamp(Mathf.FloorToInt(player.position.x - path.LaneMinX), 0, path.Width - 1);
            }
        }
        float x, z;
        path.WorldXZ(col, lane, out x, out z);
        world = new Vector3(x + 0.5f, path.BeltHeight(col) * 0.5f - 0.65f, z + 0.5f);
        return true;
    }

    static float Pitch(HSEscalatorConfigData d)
    {
        int gear = HSEscalatorConfigData.SpeedGear(d != null ? d.Speed : HSEscalatorConfigData.BaseSpeed);
        if (gear >= 3) return 1.22f;
        if (gear == 2) return 1.08f;
        return 0.96f;
    }

    static AudioClip Clip()
    {
        if (drive != null) return drive;
        const int rate = 22050;
        const int samples = rate * 2;
        var data = new float[samples];
        var rng = new System.Random(7);
        float n = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float motor = 0.42f * Mathf.Sin(2f * Mathf.PI * 55f * t)
                + 0.22f * Mathf.Sin(2f * Mathf.PI * 110f * t)
                + 0.1f * Mathf.Sin(2f * Mathf.PI * 165f * t);
            float raw = (float)(rng.NextDouble() * 2.0 - 1.0);
            n += 0.12f * (raw - n);
            float cycle = t * 5.5f;
            float frac = cycle - Mathf.Floor(cycle);
            float tick = 0f;
            if (frac < 0.04f)
                tick = (1f - frac / 0.04f) * (frac < 0.008f ? 0.35f : 0.16f);
            data[i] = Mathf.Clamp(motor + n * 0.38f + tick, -1f, 1f) * 0.55f;
        }
        drive = AudioClip.Create("HSEscalatorDrive", samples, 1, rate, false);
        drive.SetData(data, 0);
        return drive;
    }
}
