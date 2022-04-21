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
        List<IMyInventory> _inventories;
        List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>();
        List<IMyGasTank> _o2Tanks = new List<IMyGasTank>();

        float _currentO2 => _o2Tanks.Select(t => (float) t.FilledRatio * t.Capacity).Sum();
        float _maxO2;
        float _currentCharge => _batteries.Select(t => t.CurrentStoredPower).Sum();
        float _maxCharge;

        MyItemType _railgunAmmo;
        MyItemType _autocannonAmmo;

        int _totalRailgunAmmo => _inventories.Select(i => i.GetItemAmount(_railgunAmmo).ToIntSafe()).Sum();
        int _totalAutocannonAmmo => _inventories.Select(i => i.GetItemAmount(_autocannonAmmo).ToIntSafe()).Sum();


        Vector3 _velocity => _cockpit.GetShipVelocities().LinearVelocity;
        Vector2 _flatVelocity = Vector2.Zero;
        bool _prograde = true;

        readonly RectangleF _hudViewport;
        readonly Vector2 HorizonSize = new Vector2(100, 100);
        readonly Vector2 VelocitySize = new Vector2(16, 16);

        bool _wasShooting = false;
        DateTime _lastShot = DateTime.MinValue;
        RailGunState _railgunState = RailGunState.Ready;
        float _railgunCharge = 0f;

        public Program()
        {
            var railguns = new List<IMySmallMissileLauncherReload>();
            GridTerminalSystem.GetBlocksOfType(railguns);
            _railgun = railguns.First();

            GridTerminalSystem.GetBlocksOfType(_o2Tanks, t => t.CubeGrid == Me.CubeGrid);
            _maxO2 = _o2Tanks.Select(t => t.Capacity).Sum();

            GridTerminalSystem.GetBlocksOfType(_batteries, t => t.CubeGrid == Me.CubeGrid);
            _maxCharge = _batteries.Select(t => t.MaxStoredPower).Sum();

            var containers = new List<IMyEntity>();
            GridTerminalSystem.GetBlocksOfType(containers, c => c.HasInventory);
            _inventories = containers.Select(c => c.GetInventory()).ToList();

            var cockpits = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(cockpits, c => c.IsMainCockpit);
            if (cockpits.Count != 1) throw new Exception($"Must have one main cockpit, actual {cockpits.Count}");
            _cockpit = cockpits.First();
            _hudLcd = _cockpit.GetSurface(0);
            _testLcd = _cockpit.GetSurface(1);

            _hudViewport = new RectangleF(
                (_hudLcd.TextureSize - _hudLcd.SurfaceSize) / 2f,
                _hudLcd.SurfaceSize);



            _railgunAmmo = MyItemType.Parse("MyObjectBuilder_AmmoMagazine/SmallRailgunAmmo");
            _autocannonAmmo = MyItemType.Parse("MyObjectBuilder_AmmoMagazine/AutocannonClip");

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
                $"{Runtime.LastRunTimeMs}\n" +
                $"{_lastShot}\n" +
                $"{_hudViewport}\n" +
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

            _railgunCharge = GetRailgunCharge();

            _wasShooting = _railgun.IsShooting;
        }
        float GetRailgunCharge()
        {
            var seconds = (float)(DateTime.Now - _lastShot).TotalSeconds;
            if (seconds > 20) return 1f;
            if (seconds < 5) return 0f;
            return (seconds - 5) / 15f;
        }

        void DrawHud(ref MySpriteDrawFrame frame)
        {

            var background = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "White screen",
                Position = _hudViewport.Position + _hudViewport.Size / 2f,
                Size = _hudViewport.Size,
                Color = Color.Black,
                Alignment = TextAlignment.CENTER
            };
            frame.Add(background);

            var bigBar = new MyBarSprite
            {
                Position = _hudViewport.Position + _hudViewport.Size / 2f,
                Size = new Vector2(_hudViewport.Height, _hudViewport.Width),
                Color = Color.White,
                Ratio = _railgunCharge,
                Rotation = (float)Math.PI * 3 / 2,
            };
            frame.Add(bigBar);

            DrawHorizonBackground(frame);
            DrawHorizon(frame);

            var chargeText = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = _railgunState == RailGunState.Charging ? $"{_railgunCharge:p0}" : _railgunState.ToString(),
                Position = _hudViewport.Position + new Vector2((HorizonSize.X + _hudViewport.Width) / 2f, 7),
                RotationOrScale = 0.5f,
                Color = _railgunState == RailGunState.Ready ? Color.Lime.Alpha(0.66f) : Color.OrangeRed.Alpha(0.66f),
                Alignment = TextAlignment.RIGHT,
                FontId = "White"
            };
            frame.Add(chargeText);

            var ammoText = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = $"|||: {_totalRailgunAmmo}",
                Position = _hudViewport.Position + new Vector2((- HorizonSize.X + _hudViewport.Width) / 2f, 7),
                RotationOrScale = 0.5f,
                Color = Color.White.Alpha(0.66f),
                Alignment = TextAlignment.LEFT,
                FontId = "White"
            };
            frame.Add(ammoText);

            var o2 = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "IconOxygen",
                Position = _hudViewport.Position + _hudViewport.Size / 2f,
                Size = new Vector2(110, _hudViewport.Height),
                Color = Color.Black,
                Alignment = TextAlignment.CENTER
            };





        }

        private MySpriteDrawFrame DrawHorizonBackground(MySpriteDrawFrame frame)
        {
            var sq1 = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "White screen",
                Position = _hudViewport.Position + _hudViewport.Size / 2f,
                Size = new Vector2(110, _hudViewport.Height),
                Color = Color.Black,
                Alignment = TextAlignment.CENTER
            };
            frame.Add(sq1);

            var tri1 = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "Triangle",
                Position = _hudViewport.Position + _hudViewport.Size / 2f + new Vector2(-55, 0),
                Size = new Vector2(40, _hudViewport.Height * 1.2f),
                Color = Color.Black,
                Alignment = TextAlignment.CENTER
            };
            frame.Add(tri1);

            var tri2 = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "Triangle",
                Position = _hudViewport.Position + _hudViewport.Size / 2f + new Vector2(55, 0),
                Size = new Vector2(40, _hudViewport.Height * 1.2f),
                Color = Color.Black,
                Alignment = TextAlignment.CENTER
            };
            frame.Add(tri2);
            return frame;
        }

        private MySpriteDrawFrame DrawHorizon(MySpriteDrawFrame frame)
        {
            var horizon = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "CircleHollow",
                Position = _hudViewport.Position + _hudViewport.Size / 2f,
                Size = HorizonSize,
                Color = Color.White.Alpha(0.66f),
                Alignment = TextAlignment.CENTER
            };
            frame.Add(horizon);

            var heading = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "AH_BoreSight",
                Position = _hudViewport.Position + _hudViewport.Size / 2f + new Vector2(0, 3),
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
                Position = _hudViewport.Position + _hudViewport.Size / 2f + 50 * _flatVelocity,
                Size = VelocitySize,
                RotationOrScale = _prograde ? 0f : (float)Math.PI,
                Color = _prograde ? Color.Lime.Alpha(0.66f) : Color.OrangeRed.Alpha(0.66f),
                Alignment = TextAlignment.CENTER,
            };
            frame.Add(velocity);
            return frame;
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
