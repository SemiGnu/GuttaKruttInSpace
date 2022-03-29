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
        IMyTextSurface _cockpitLcd2;
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
            _cockpitLcd2 = _cockpit.GetSurface(0);

            _viewport = new RectangleF(
                (_cockpitLcd2.TextureSize - _cockpitLcd2.SurfaceSize) / 2f,
                _cockpitLcd2.SurfaceSize);



            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            _tick++;
            UpdatePos();

            if (_tick % 10 != 0) return;

            var frame = _cockpitLcd2.DrawFrame();
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
