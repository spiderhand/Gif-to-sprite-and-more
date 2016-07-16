using UnityEditor;
using UnityEngine;

// Editor window for listing all object reference curves in an animation clip
public class ClipInfo : EditorWindow
{
    private AnimationClip clip;

    [MenuItem("Window/Clip Info")]
    static void Init()
    {
        GetWindow(typeof(ClipInfo));
    }

    public void OnGUI()
    {
        clip = EditorGUILayout.ObjectField("Clip", clip, typeof(AnimationClip), false) as AnimationClip;

        EditorGUILayout.LabelField("Object reference curves:");
        if (clip != null)
        {
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                EditorGUILayout.LabelField(binding.path + "/" + binding.propertyName + ", Keys: " + keyframes.Length);
            }
        }
    }
}