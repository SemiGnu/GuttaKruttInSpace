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
        IMyTextSurface _lcd;
        IMyBroadcastListener _listener;
        IMyPistonBase[] _pistons = new IMyPistonBase[3];
        IMyShipConnector _connector;
        IMyShipController _helm;

        MatrixD _targetWorldMatrix = MatrixD.Zero;
        Vector3D _targetVector = Vector3D.Zero;

        string _autoConnectorTagBroadcastTag = "AUTO_CONNECTOR_LISTENER";

        string _test = "idle";

        bool Manual = false;

        public Program()
        {
            _connector = GridTerminalSystem.GetBlockWithName("Auto Connector") as IMyShipConnector;
            _helm = GridTerminalSystem.GetBlockWithName("Auto Connector Helm") as IMyShipController;
            _pistons[0] = GridTerminalSystem.GetBlockWithName("Auto Connector Piston Up") as IMyPistonBase;
            _pistons[1] = GridTerminalSystem.GetBlockWithName("Auto Connector Piston Left") as IMyPistonBase;
            _pistons[2] = GridTerminalSystem.GetBlockWithName("Auto Connector Piston Forward") as IMyPistonBase;

            _lcd = (_helm as IMyTextSurfaceProvider).GetSurface(1);

            _listener = IGC.RegisterBroadcastListener(_autoConnectorTagBroadcastTag);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {
            
        }

        public void Main(string argument, UpdateType updateSource)
        {
            HandleMessages();
            CalculateTargetVector();
            MovePistons();
            Connect();
            _lcd.WriteText($"{_test}\n{_targetVector}");
        }

        private void Connect()
        {
            if (_targetVector == Vector3D.Zero) return;
            if (_connector.Status == MyShipConnectorStatus.Connectable)
            {
                _connector.Connect();
                _targetVector = Vector3D.Zero;
                _targetWorldMatrix = MatrixD.Zero;
                _pistons[0].Velocity = 0f;
                _pistons[1].Velocity = 0f;
                _pistons[2].Velocity = 0f;
            }
        }

        private void MovePistons()
        {
            if (_targetVector == Vector3D.Zero && _connector.Status != MyShipConnectorStatus.Connected)
            {
                _pistons[0].Velocity = -1f;
                _pistons[1].Velocity = -1f;
                _pistons[2].Velocity = -1f;
                return;
            }
            _pistons[0].Velocity = Math.Abs(_targetVector.Y) > 0.1 ? 1f : 0f;
            _pistons[1].Velocity = Math.Abs(_targetVector.X) > 0.1 ? 1f : 0f;
            if (_pistons[0].Velocity == 0f && _pistons[1].Velocity == 0f)
            {
                _pistons[2].Velocity = Math.Abs(_targetVector.Z) > 0.1 ? 1f : 0f;
            }
        }
        private void CalculateTargetVector()
        {
            if (_targetWorldMatrix == MatrixD.Zero) return;
            var distance = _targetWorldMatrix.Translation - _connector.WorldMatrix.Translation;
            _targetVector = Vector3D.TransformNormal(distance, MatrixD.Transpose(_helm.WorldMatrix));
        }

        private void HandleMessages()
        {
            while (_listener.HasPendingMessage)
            {
                MyIGCMessage message = _listener.AcceptMessage();
                if (message.Tag == _autoConnectorTagBroadcastTag)
                {
                    if (_targetWorldMatrix != MatrixD.Zero || _connector.Status == MyShipConnectorStatus.Connected)
                    {
                        _connector.Disconnect();
                        _targetVector = Vector3D.Zero;
                        _targetWorldMatrix = MatrixD.Zero;
                        continue;
                    }
                    if (message.Data is MatrixD)
                    {
                        var matrix = (MatrixD)message.Data;
                        var localMatrix = _connector.WorldMatrix;
                        var distance = Vector3D.Distance(matrix.Translation, localMatrix.Translation);
                        _test = distance.ToString();
                        if (distance < double.MaxValue)
                        {
                            _targetWorldMatrix = matrix;
                            
                        }
                    }
                }
            }
        }
    }
}
