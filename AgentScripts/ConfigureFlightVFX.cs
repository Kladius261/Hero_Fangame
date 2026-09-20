using UnityEngine;
using UnityEditor;

public static class ConfigureFlightVFX
{
    static Material CloneOrLoadMaterial(string sourcePath, string newPath, Color baseColor)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(newPath);
        if (mat == null)
        {
            AssetDatabase.CopyAsset(sourcePath, newPath);
            AssetDatabase.ImportAsset(newPath);
            mat = AssetDatabase.LoadAssetAtPath<Material>(newPath);
        }
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_Color", baseColor);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject GetOrCreateChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        if (t != null)
        {
            return t.gameObject;
        }
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.AddComponent<ParticleSystem>();
        return go;
    }

    public static string Main()
    {
        string auraPath = "Assets/Materials/FlightAura.mat";
        string auraRingsPath = "Assets/Materials/FlightAuraRings.mat";
        string propPath = "Assets/Materials/FlightPropulsion.mat";
        string landPath = "Assets/Materials/FlightLanding.mat";
        string crashPath = "Assets/Materials/FlightCrash.mat";

        Material auraMat = CloneOrLoadMaterial("Assets/Materials/FreezeBreathMist.mat", auraPath, new Color(1.3f, 1.9f, 2.4f, 0.85f));
        Material auraRingsMat = CloneOrLoadMaterial("Assets/Materials/FreezeBreathMist.mat", auraRingsPath, new Color(1.6f, 2.2f, 2.7f, 1f));
        Material propMat = CloneOrLoadMaterial("Assets/Materials/FreezeBreathStreak.mat", propPath, new Color(2.2f, 3.0f, 4.0f, 1f));
        Material landMat = CloneOrLoadMaterial("Assets/Materials/FreezeBreathMist.mat", landPath, new Color(1.7f, 2.1f, 2.7f, 1f));
        Material crashMat = CloneOrLoadMaterial("Assets/Materials/HeatVisionSparkle.mat", crashPath, new Color(5f, 6.2f, 7.5f, 1f));

        AssetDatabase.SaveAssets();

        var player = GameObject.Find("/Player");

        ConfigureAura(GameObject.Find("/Player/Aura"), auraMat);
        ConfigureAuraRings(GetOrCreateChild(player, "AuraRings"), auraRingsMat);
        ConfigurePropulsion(GameObject.Find("/Player/Propulsion"), propMat);
        ConfigureLanding(GameObject.Find("/Player/Landing"), landMat);
        ConfigureLandingDebris(GetOrCreateChild(player, "LandingDebris"), crashMat);
        ConfigureCrash(GameObject.Find("/Player/Crash"), crashMat);
        ConfigureChargeDebris(GetOrCreateChild(player, "ChargeDebris"), crashMat);

        return "OK";
    }

    static void ConfigureAura(GameObject go, Material mat)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var pr = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        pr.sharedMaterial = mat;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sortingLayerName = "Default";
        pr.sortingOrder = 3;

        var main = ps.main;
        main.loop = true;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
        main.startColor = new Color(0.75f, 0.95f, 1f, 0.85f);
        main.startRotation3D = false;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 200;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 22f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.45f;
        shape.radiusThickness = 0f;
        shape.arc = 360f;

        // Downward-flowing mist/shards: steeper downward drift than a
        // purely ambient swirl, plus a gentle orbit so it still reads as
        // "aura" rather than simple falling snow.
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.Local;
        vol.orbitalY = new ParticleSystem.MinMaxCurve(0.7f);
        vol.y = new ParticleSystem.MinMaxCurve(-0.4f);

        // Individual shard/mote glint — a slow random tumble on each
        // particle sells small ice shards catching the light as they orbit
        // and fall, without needing a second sprite.
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.85f, 1f, 1.3f));

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(ps);
        EditorUtility.SetDirty(pr);
    }

    static void ConfigureAuraRings(GameObject go, Material mat)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var pr = go.GetComponent<ParticleSystemRenderer>() ?? go.AddComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        pr.sharedMaterial = mat;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sortingLayerName = "Default";
        pr.sortingOrder = 2;

        var main = ps.main;
        main.loop = true;
        main.duration = 0.7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.16f);
        main.startColor = new Color(0.85f, 0.98f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 80;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        // A ring "pulse" every 0.7s for as long as the system plays —
        // repeatInterval/cycleCount=0 means it auto-repeats without any
        // extra scripting on FlightAbility's side.
        var burst = new ParticleSystem.Burst(0f, 46)
        {
            cycleCount = 0,
            repeatInterval = 0.7f,
        };
        emission.SetBursts(new[] { burst });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.85f;
        shape.radiusThickness = 0f;
        shape.arc = 360f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g;

        // Dots grow slightly over their short life so the ring silhouette
        // reads as a subtle outward expansion/pulse, not a static dashed
        // circle.
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 1.4f));

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(ps);
        EditorUtility.SetDirty(pr);
    }

    static void ConfigurePropulsion(GameObject go, Material mat)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var pr = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        pr.sharedMaterial = mat;
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.lengthScale = 7f;
        pr.velocityScale = 0.5f;
        pr.sortingLayerName = "Default";
        pr.sortingOrder = 6;

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        // Small initial speed only — the real "upward burst" motion comes
        // from velocityOverLifetime below so every particle commits hard to
        // a vertical column instead of a directionless sphere burst.
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
        main.startColor = new Color(0.8f, 0.94f, 1f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 140;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;
        shape.radiusThickness = 1f;

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.Local;
        vol.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
        vol.y = new ParticleSystem.MinMaxCurve(8f, 14f);
        vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g2 = new Gradient();
        g2.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.85f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g2;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(ps);
        EditorUtility.SetDirty(pr);
    }

    static void ConfigureLanding(GameObject go, Material mat)
    {
        // Reworked from a scattering dust puff into a downward shockwave
        // impact: particles are born clustered near the player's feet and
        // rocket outward together along the ground plane (with a brief
        // downward shove first), so the *group* silhouette reads as a
        // fast-expanding ring/wave rather than individual debris flying
        // off in random directions.
        var ps = go.GetComponent<ParticleSystem>();
        var pr = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        pr.sharedMaterial = mat;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sortingLayerName = "Default";
        pr.sortingOrder = 4;

        var main = ps.main;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startColor = new Color(0.9f, 0.96f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 60;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        // Thin ring at the feet — starting all particles on the same tight
        // circle (rather than filling a disc) is what makes the burst read
        // as a single expanding wavefront instead of a cloud.
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;
        shape.radiusThickness = 0f;
        shape.arc = 360f;

        // A short downward shove as the wave kicks off, on top of the
        // shape's outward radial velocity — sells the sense of the impact
        // slamming down before rippling outward along the ground.
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.Local;
        vol.y = new ParticleSystem.MinMaxCurve(-1.2f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g3 = new Gradient();
        g3.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.25f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g3;

        // Grows fast as the wave crests, then collapses away — a "pulse"
        // rather than the steady growth used for the ambient AuraRings.
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.25f, 1.4f),
            new Keyframe(1f, 0f));
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(ps);
        EditorUtility.SetDirty(pr);
    }

    static void ConfigureLandingDebris(GameObject go, Material mat)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var pr = go.GetComponent<ParticleSystemRenderer>() ?? go.AddComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        pr.sharedMaterial = mat;
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.lengthScale = 1.5f;
        pr.velocityScale = 0.3f;
        pr.sortingLayerName = "Default";
        pr.sortingOrder = 5;

        var main = ps.main;
        main.loop = false;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.playOnAwake = false;
        // Local to the particle system only — independent of the player's
        // Rigidbody2D.gravityScale (which stays 0). Lets chips arc up and
        // fall back down for a "kicked debris" motion.
        main.gravityModifier = 1.2f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;
        shape.radiusThickness = 1f;

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vol.y = new ParticleSystem.MinMaxCurve(3f, 6f);
        vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g4 = new Gradient();
        g4.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.8f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g4;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.6f));

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(ps);
        EditorUtility.SetDirty(pr);
    }

    static void ConfigureChargeDebris(GameObject go, Material mat)
    {
        // Same debris-chip look as LandingDebris (Stretch-rendered
        // fragments), but with the world-up bias and gravity removed so
        // the chips sparkle symmetrically outward from the impact point
        // instead of popping up and falling — matches a Charge slamming
        // into something rather than a vertical landing.
        var ps = go.GetComponent<ParticleSystem>();
        var pr = go.GetComponent<ParticleSystemRenderer>() ?? go.AddComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        pr.sharedMaterial = mat;
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.lengthScale = 2.2f;
        pr.velocityScale = 0.3f;
        pr.sortingLayerName = "Default";
        pr.sortingOrder = 21;

        var main = ps.main;
        main.loop = false;
        main.duration = 0.45f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 13f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.playOnAwake = false;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 34) });

        // Full-sphere radial emission (no dominant axis) — the shape's
        // own outward velocity is what carries the "sparkle from center"
        // motion, with no extra velocityOverLifetime bias layered on top.
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.18f;
        shape.radiusThickness = 1f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g6 = new Gradient();
        g6.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.8f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g6;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(ps);
        EditorUtility.SetDirty(pr);
    }

    static void ConfigureCrash(GameObject go, Material mat)
    {
        var ps = go.GetComponent<ParticleSystem>();
        var pr = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        pr.sharedMaterial = mat;
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.lengthScale = 2.5f;
        pr.velocityScale = 0.25f;
        pr.sortingLayerName = "Default";
        pr.sortingOrder = 20;

        var main = ps.main;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.03f;
        shape.arc = 360f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g5 = new Gradient();
        g5.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.8f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g5;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(ps);
        EditorUtility.SetDirty(pr);
    }
}
