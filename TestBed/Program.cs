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
    partial class Program : MyGridProgram
    {
        IMyCockpit _cockpit;
        IMyTextSurface _cockpitLcd1;
        IMyTextSurface _hudLcd;
        RectangleF _viewport;

        uint _tick = 0;
        Vector2 _cursor = new Vector2(0, 0);

        public Program()
        {

            var cockpits = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(cockpits, c => c.IsMainCockpit);
            if (cockpits.Count != 1) throw new Exception($"Must have one main cockpit, actual {cockpits.Count}");
            _cockpit = cockpits.First();
            _cockpitLcd1 = _cockpit.GetSurface(1);
            _hudLcd = _cockpit.GetSurface(0);

            _viewport = new RectangleF(
                (_hudLcd.TextureSize - _hudLcd.SurfaceSize) / 2f,
                _hudLcd.SurfaceSize);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            //UpdateCursor();
            var frame = _hudLcd.DrawFrame();


            var circle = new MySprite
            {
                Color = Color.Black,
                Size = new Vector2(100, 100),
                Position = _viewport.Position + _viewport.Size / 2f,
                Data = "Triangle",
                RotationOrScale = (float) Math.PI / 4,
                Alignment = TextAlignment.CENTER,
            };
            frame.Add(circle);


            var bar = new MyBarSprite
            {
                Position = _viewport.Position + _viewport.Size / 2f,
                Size = new Vector2(100, 20),
                Color = Color.White,
                Ratio = (_tick++ % 20) / 20f,
                Rotation = (float) Math.PI / 4,
            };

            var sprite = (MySprite)bar;

            frame.Add(bar);

            frame.Dispose();


            _cockpitLcd1.WriteText($"{sprite.Size}\n{sprite.Position}");
        }

        private void UpdateCursor()
        {
            _tick++;
            UpdatePos();

            if (_tick % 10 != 0) return;

            var frame = _hudLcd.DrawFrame();
            DrawCursor(ref frame);
            frame.Dispose();

            var status = $"TEST\n" +
                $"\n{_cockpit.MoveIndicator}" +
                $"\n{_cockpit.RollIndicator}" +
                $"\n{_cockpit.RotationIndicator}" +
                $"\n\n{_cursor}" +
                $"\n{_viewport}";
            _cockpitLcd1.WriteText(status);
        }

        private void DrawCursor(ref MySpriteDrawFrame frame)
        {
            Echo("frame");
            var sprite = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "AH_BoreSight",
                Position = _cursor,
                RotationOrScale = -((float)Math.PI / 2),
                Size = _viewport.Size * 0.1f,
                Color = Color.White,
                Alignment = TextAlignment.CENTER
            };
            // Add the sprite to the frame
            frame.Add(sprite);
        }

        public void UpdatePos()
        {
            var rot = _cockpit.RotationIndicator;
            rot.Rotate(Math.PI / 2);
            rot = Vector2.Reflect(rot, Vector2.UnitX);


            _cursor += rot *= 0.2f;
            _cursor = Vector2.Clamp(_cursor, Vector2.Zero, _viewport.Size);

        }
    }
}
