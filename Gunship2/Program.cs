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
        IMyTextSurface _hudLcd;
        IMyTextSurface _testLcd;
        IMySmallMissileLauncherReload _railgun;

        Vector3 _velocity => _cockpit.GetShipVelocities().LinearVelocity;
        Vector2 _flatVelocity = Vector2.Zero;
        bool _prograde = true;

        readonly RectangleF _viewport;
        readonly Vector2 HorizonSize = new Vector2(100, 100);
        readonly Vector2 VelocitySize = new Vector2(16, 16);

        bool _wasShooting = false;
        DateTime _lastShot = DateTime.MinValue;
        RailGunState _railgunState = RailGunState.Ready;

        public Program()
        {
            var railguns = new List<IMySmallMissileLauncherReload>();
            GridTerminalSystem.GetBlocksOfType(railguns);
            _railgun = railguns.First();

            var cockpits = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(cockpits, c => c.IsMainCockpit);
            if (cockpits.Count != 1) throw new Exception($"Must have one main cockpit, actual {cockpits.Count}");
            _cockpit = cockpits.First();
            _hudLcd = _cockpit.GetSurface(0);
            _testLcd = _cockpit.GetSurface(1);

            _viewport = new RectangleF(
                (_hudLcd.TextureSize - _hudLcd.SurfaceSize) / 2f,
                _hudLcd.SurfaceSize);



            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            CalculateTargetVector();
            CheckRailgunStatus();
            var frame = _hudLcd.DrawFrame();
            DrawHud(ref frame);
            frame.Dispose();

            var status = $"TEST\n" +
                $"{_lastShot}\n" +
                $"{_viewport}\n" +
                $"{GetRailgunCharge()}";
            _testLcd.WriteText(status);
            Echo(status);
        }

        void CheckRailgunStatus()
        {
            if (_railgun.IsShooting && !_wasShooting)
            {
                _lastShot = DateTime.Now;
            }
            var timeSinceLastShot = (DateTime.Now - _lastShot).TotalSeconds;

            if (timeSinceLastShot > 20) _railgunState = RailGunState.Ready;
            else if (timeSinceLastShot > 5) _railgunState = RailGunState.Charging;
            else if (timeSinceLastShot > 1) _railgunState = RailGunState.Reloading;
            else _railgunState = RailGunState.Firing;

            _wasShooting = _railgun.IsShooting;
        }

        void DrawHud(ref MySpriteDrawFrame frame)
        {
            var horizon = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "CircleHollow",
                Position = _viewport.Position + _viewport.Size / 2f,
                Size = HorizonSize,
                Color = Color.White.Alpha(0.66f),
                Alignment = TextAlignment.CENTER
            };
            frame.Add(horizon);

            var heading = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "AH_BoreSight",
                Position = _viewport.Position + _viewport.Size / 2f + new Vector2(0,3),
                Size = VelocitySize,
                RotationOrScale = -((float)Math.PI / 2),
                Color = Color.White.Alpha(0.66f),
                Alignment = TextAlignment.CENTER
            };
            frame.Add(heading);

            var velocity = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "AH_VelocityVector",
                Position = _viewport.Position + _viewport.Size / 2f + 50 * _flatVelocity,
                Size = VelocitySize,
                RotationOrScale = _prograde ? 0f : (float)Math.PI,
                Color = _prograde ? Color.Lime.Alpha(0.66f) : Color.OrangeRed.Alpha(0.66f),
                Alignment = TextAlignment.CENTER,
            };
            frame.Add(velocity);

            var railgunCharge = GetRailgunCharge();
            var charge = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "White screen",
                Position = _viewport.Position + new Vector2((_viewport.Width / 2f) - (50 * (1 - railgunCharge)), 15),
                Size = new Vector2(100 * railgunCharge, 16),
                Color = Color.White.Alpha(0.66f),
                Alignment = TextAlignment.CENTER,
            };
            frame.Add(charge);


            var text = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = _railgunState == RailGunState.Charging ? $"{railgunCharge:p0}" : _railgunState.ToString(),
                Position = _viewport.Position + new Vector2(_viewport.Width / 2f, 7),
                RotationOrScale = 0.5f,
                Color = _railgunState == RailGunState.Ready ? Color.Lime.Alpha(0.66f) : Color.OrangeRed.Alpha(0.66f),
                Alignment = TextAlignment.CENTER,
                FontId = "White"
            };
            var textBg = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "White screen",
                Position = _viewport.Position + new Vector2(_viewport.Width / 2f, 15),
                Size = new Vector2(50, 12),
                Color = Color.Black,
                Alignment = TextAlignment.CENTER,
            };
            frame.Add(textBg);
            frame.Add(text);



        }


        float GetRailgunCharge() {
            var seconds = (float)(DateTime.Now - _lastShot).TotalSeconds;
            if (seconds > 20) return 1f;
            if (seconds < 5) return 0f;
            return (seconds - 5) / 15f;
        }

        void CalculateTargetVector()
        {
            if (_cockpit.GetShipSpeed() < 0.01)
            {
                _flatVelocity = Vector2.Zero;
                _prograde = true;
                return;
            }
            var v = Vector3.TransformNormal(_velocity, Matrix.Transpose(_cockpit.WorldMatrix));
            _prograde = v.Z < 0.01f;
            v.Normalize();
            _flatVelocity = new Vector2(v.X, -v.Y);
        }

        enum RailGunState
        {
            Ready, Firing, Reloading, Charging
        }

    }
}
