using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class SpriteHelper
        {
            public MySprite GetBarSprite()
            {
                return new MySprite();
            }
        }

        public struct MyBarSprite
        {
            private float _ratio;

            public Vector2 Size;
            public Vector2 Position;
            public Color Color;
            public float Ratio { get { return _ratio; } set { _ratio = Math.Max(0f, Math.Min(1f, value)); } }
            public float Rotation;


            public static implicit operator MySprite(MyBarSprite bar)
            {
                var size = bar.Size;
                size.X *= bar.Ratio;

                var positionOffset = new Vector2(bar.Size.X / -2f * (1 - bar.Ratio), 0);
                positionOffset.Rotate(bar.Rotation);

                var sprite = new MySprite {
                    Color = bar.Color,
                    Type = SpriteType.TEXTURE,
                    Data = "White screen",
                    Size = size,
                    Position = bar.Position + positionOffset,
                    RotationOrScale = bar.Rotation,
                    Alignment = TextAlignment.CENTER,
                };
                return sprite;
            }

        }
    }
}
