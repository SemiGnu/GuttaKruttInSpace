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
        IMyShipConnector _topDock, _bottomDock;
        IMyRemoteControl _remote;
        MatrixD _topDockMatrix, _bottomDockMatrix;
        IMyBroadcastListener _infoListener;
        IMyUnicastListener _distanceListener;
        List<IMyTextSurface> _lcds = new List<IMyTextSurface>();

        string _capsuleInfoBroadcastTag = "CAPSULE_INFO_LISTENER";
        string _capsuleDistanceBroadcastTag = "CAPSULE_DISTANCE_LISTENER";
        string _unicastInfoTag = "CAPSULE_INFO_LISTENER";
        string _unicastTriggerTag = "CAPSULE_TRIGGER_LISTENER";
        long _capsuleId;

        double _dockingOffset = 1.85;
        string _status;

        public Program()
        {
            _topDock = GridTerminalSystem.GetBlockWithName("Capsule Top Dock") as IMyShipConnector;
            _bottomDock = GridTerminalSystem.GetBlockWithName("Capsule Bottom Dock") as IMyShipConnector;
            _remote = GridTerminalSystem.GetBlockWithName("Capsule Hub Remote Control") as IMyRemoteControl;

            var tsps = new List<IMyTextSurfaceProvider>();
            GridTerminalSystem.GetBlocksOfType(tsps);
            _lcds = tsps.Where(t => (t as IMyTerminalBlock).CustomName.StartsWith("Capsule LCD"))?.Select(t => t.GetSurface(0)).ToList();
            _lcds.Add(Me.GetSurface(0));

            _infoListener = IGC.RegisterBroadcastListener(_capsuleInfoBroadcastTag);
            //_infoListener.SetMessageCallback(_capsuleInfoBroadcastTag);            
            _distanceListener = IGC.UnicastListener;
            //_distanceListener.SetMessageCallback(_capsuleDistanceBroadcastTag);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            HandleMessages();
            if ((updateSource & UpdateType.Trigger) > 0)
            {
                IGC.SendUnicastMessage(_capsuleId, _unicastTriggerTag, argument);
            }
            string status = $"{_infoListener.HasPendingMessage}";
            if (_bottomDock.Status == MyShipConnectorStatus.Connected)
            {
                status = "Docked\nBase";
            } else if (_topDock.Status == MyShipConnectorStatus.Connected)
            {
                status = "Docked\nOrbit";
            } else
            {
                status = _status;
            }
            _lcds.ForEach(l => l.WriteText(status));
        }

        private void HandleMessages()
        {
            while (_infoListener.HasPendingMessage)
            {
                MyIGCMessage message = _infoListener.AcceptMessage();
                if (message.Tag == _capsuleInfoBroadcastTag)
                {
                    _capsuleId = message.Source;

                    var topMatrix = _topDock.WorldMatrix;
                    topMatrix.Translation += topMatrix.Forward * _dockingOffset;
                    _topDockMatrix = MatrixD.CreateFromDir(topMatrix.Backward);
                    _topDockMatrix.Translation = topMatrix.Translation;
                    var bottomMatrix = _bottomDock.WorldMatrix;
                    bottomMatrix.Translation += bottomMatrix.Forward * _dockingOffset;
                    _bottomDockMatrix = MatrixD.CreateFromDir(bottomMatrix.Backward);
                    _bottomDockMatrix.Translation = bottomMatrix.Translation;

                    var remoteQuaternion = QuaternionD.CreateFromForwardUp(_remote.WorldMatrix.Forward, _remote.WorldMatrix.Up);

                    IGC.SendUnicastMessage(message.Source, _unicastInfoTag, MyTuple.Create(topMatrix, bottomMatrix, remoteQuaternion));
                }
            }

            while (_distanceListener.HasPendingMessage)
            {
                MyIGCMessage message = _distanceListener.AcceptMessage();
                if (message.Tag == _capsuleDistanceBroadcastTag)
                {
                    if (message.Data is string)
                    {
                        _status = (string)message.Data;
                    }
                }
            }
        }
    }
}
