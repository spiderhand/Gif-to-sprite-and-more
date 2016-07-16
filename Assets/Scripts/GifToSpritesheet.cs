using System; 
using System.Drawing; 
using System.Drawing.Imaging; 
using System.Collections.Generic; 
using System.IO; 
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts
{
    public class GifToSpriteWindow : EditorWindow
    { 
        public UnityEngine.Object source;
        public int padding;

        [MenuItem("Window/GifToSprite")]
        public static void ShowWindow() 
        {
            EditorWindow.GetWindow(typeof(GifToSpriteWindow), false, "GifToSprite");
        }

        void OnGUI() 
        {
            
            GUILayout.Label("Base settings", EditorStyles.boldLabel);
            source = EditorGUILayout.ObjectField("Gif asset", source, typeof(UnityEngine.Object), false);
            padding = EditorGUILayout.IntField("Padding", padding);

            if (GUILayout.Button("Convert"))
            if (source == null)
                ShowNotification(new GUIContent("No object selected for searching"));
            else
            {
                string path = AssetDatabase.GetAssetPath(source);
                GifToSprite.Convert(path, padding);
            }
                
        }
    }

    public class GifTextureImport : AssetPostprocessor
    {
        TextureImporter textureImporter;
        void OnPreprocessTexture()
        {
            textureImporter = (TextureImporter)assetImporter;
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Multiple;
            textureImporter.mipmapEnabled = false;
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.maxTextureSize = 2048;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
        }

        public void OnPostprocessTexture(Texture2D texture)
        {
            Debug.Log("Texture2D: (" + texture.width + "x" + texture.height + ")");

            var spriteWidth = GifToSprite.width;
            var spriteHeight = GifToSprite.height;

            int colCount = texture.width / spriteWidth;
            int rowCount = texture.height / spriteHeight;

            List<SpriteMetaData> metas = new List<SpriteMetaData>();

            for (int r = 0; r < rowCount; ++r)
            {
                for (int c = 0; c < colCount; ++c)
                {
                    SpriteMetaData meta = new SpriteMetaData();
                    meta.rect = new Rect(c * (spriteWidth + GifToSprite.padding), r * (spriteHeight + GifToSprite.padding), spriteWidth, spriteHeight);
                    meta.name = c + "-" + r;
                    metas.Add(meta);
                }
            }

            TextureImporter textureImporter = (TextureImporter)assetImporter;
            textureImporter.spritesheet = metas.ToArray();
        }
    }

    public static class GifToSprite
    {
        public static int padding;
        public static int width;
        public static int height;

        public static void Convert(string path, int pad) 
        {
            padding = pad;
            var images = new List<GifFrame>();
            var img = new Bitmap(path);
            width = img.Width;
            height = img.Height;
            var frames = img.GetFrameCount(FrameDimension.Time); 
            if (frames <= 1) throw new ArgumentException("Image not animated"); 
            var times = img.GetPropertyItem(0x5100).Value; 
            int frame = 0; 
            for (; ; ) 
            { 
                float dur = BitConverter.ToInt32(times, 4 * frame);
                var curr = new Texture2D(img.Width, img.Height);
                for (int i = 0; i < curr.height; i++)
                {
                    for (int j = 0; j < curr.width; j++)
                    {
                        var color = img.GetPixel(j, i);
                        var unityColor = new UnityEngine.Color32(color.R, color.G, color.B, color.A);
                        curr.SetPixel(j, curr.height - i, unityColor);                        
                    }
                }
                curr.Apply();
                //var sprite = Sprite.Create(curr, new Rect(0,0,curr.width,curr.height), new Vector2(0.5f,0.5f));
                images.Add(new GifFrame(curr, dur));
                if (++frame >= frames) break; 
                img.SelectActiveFrame(FrameDimension.Time, frame); 
            } 
            img.Dispose();

            CreateSpritesheet(images, padding);
            AssetDatabase.ImportAsset("Assets/SavedScreen.png");

            var temp = new GameObject();

            var spriteRend = temp.AddComponent<SpriteRenderer>();
            var anim = temp.AddComponent<Animator>();
            //var anim = temp.GetComponent<Animator>();
            
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath("Assets/StateMachineTransitions.controller");
            //var spriteRend = temp.GetComponent<SpriteRenderer>();            
            var prefab = PrefabUtility.CreatePrefab("Assets/1.prefab", temp);

            var sprites = Resources.LoadAll<Sprite>("SavedScreen");

            AnimationClip animClip = new AnimationClip();
            animClip.frameRate = 1 / (images[0].Duration / 100);   // FPS
            animClip.name = "Play";
            EditorCurveBinding spriteBinding = new EditorCurveBinding();
            spriteBinding.type = typeof(SpriteRenderer);
            spriteBinding.path = "";
            spriteBinding.propertyName = "m_Sprite";
            ObjectReferenceKeyframe[] spriteKeyFrames = new ObjectReferenceKeyframe[images.Count];
            for (int i = 0; i < sprites.Length; i++)
            {
                spriteKeyFrames[i] = new ObjectReferenceKeyframe();
                spriteKeyFrames[i].time = i * images[i].Duration / 100;
                spriteKeyFrames[i].value = sprites[i];
            }

            AnimationUtility.SetObjectReferenceCurve(animClip, spriteBinding, spriteKeyFrames);

            spriteRend.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SavedScreen.png");
            controller.AddMotion(animClip);
            anim.runtimeAnimatorController = controller;

            AssetDatabase.CreateAsset(animClip, "Assets/1.anim");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            PrefabUtility.ReplacePrefab(temp, prefab);
            SceneAsset.DestroyImmediate(temp);

            padding = 0;
            width = 0;
            height = 0;
        }


        public static void CreateSpritesheet(List<GifFrame> images, int padding) {       

            var width = images[0].Image.width;
            var height = images[0].Image.height;

            var cols = 2048 / (width + padding);
            if (cols > images.Count)
                cols = images.Count;
            var remainder = images.Count % cols > 0 ? 1 : 0;
            var rows = images.Count / cols + remainder;

            var totalWidthWithPadding = cols * width + (cols - 1) * padding;
            var totalHeightWithPadding = rows * height + (rows - 1) * padding;

            var spritesheet = new Texture2D(totalWidthWithPadding, totalHeightWithPadding);

            for (int r = 0; r < rows; ++r)
            {
                for (int c = 0; c < cols; ++c)
                {
                    var curr = images[r * cols + c].Image;
                    for (int i = 0; i < curr.height; i++)
                    {
                        for (int j = 0; j < curr.width; j++)
                        {
                            var color = curr.GetPixel(j, i);
                            //var unityColor = new UnityEngine.Color((byte)color.r, (byte)color.g, (byte)color.b, (byte)color.a);
                            spritesheet.SetPixel(c * (width + padding) + j, r * (height+padding) + i, color);
                        }
                    }                    
                }
            }

            spritesheet.Apply();

            File.WriteAllBytes(Application.dataPath + "/SavedScreen.png", spritesheet.EncodeToPNG());

            //List<SpriteMetaData> metas = new List<SpriteMetaData>();

            //for (int r = 0; r < rows; ++r)
            //{
            //    for (int c = 0; c < cols; ++c)
            //    {
            //        SpriteMetaData meta = new SpriteMetaData();
            //        meta.rect = new Rect(c * (width + padding), r * (height + padding), width, height);
            //        meta.name = c + "-" + r;
            //        metas.Add(meta);
            //    }
            //}

            //TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(Application.dataPath + "/SavedScreen.png");
            //textureImporter.spritesheet = metas.ToArray();
            //var spritesheet = new Texture2D(totalWidthWithPadding, totalHeightWithPadding);
            //var textures = images.Select((x) => x.Image).ToArray();
            ////spritesheet.PackTextures(textures, padding);

            //List<SpriteMetaData> metas = new List<SpriteMetaData>();
            //for (int r = 0; r < rows; ++r)
            //{
            //    for (int c = 0; c < cols; ++c)
            //    {
            //        SpriteMetaData meta = new SpriteMetaData();
            //        meta.rect = new Rect(c * (width + padding), r * (height + padding), width, height);
            //        meta.name = c + "-" + r;
            //        metas.Add(meta);
            //    }
            //}

            //TextureImporter textureImporter = new TextureImporter();
            //textureImporter.spritesheet = metas.ToArray();
        }
    }

    public class GifFrame 
    { 
        private float mDuration; 
        private Texture2D mImage; 
        internal GifFrame(Texture2D img, float duration) 
        { 
            mImage = img; mDuration = duration; 
        } 
        public Texture2D Image { get { return mImage; } } 
        public float Duration { get { return mDuration; } } 
    }
}