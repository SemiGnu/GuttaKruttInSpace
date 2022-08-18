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
        MatrixD _topDockMatrix, _bottomDockMatrix;
        IMyBroadcastListener _infoListener;
        IMyUnicastListener _distanceListener;

        string _capsuleInfoBroadcastTag = "CAPSULE_INFO_LISTENER";
        string _capsuleDistanceBroadcastTag = "CAPSULE_DISTANCE_LISTENER";
        string _unicastInfoTag = "CAPSULE_INFO_LISTENER";

        float _distance;

        public Program()
        {
            _topDockMatrix = GridTerminalSystem.GetBlockWithName("Capsule Top Dock").WorldMatrix;
            _bottomDockMatrix = GridTerminalSystem.GetBlockWithName("Capsule Bottom Dock").WorldMatrix;

            _infoListener = IGC.RegisterBroadcastListener(_capsuleInfoBroadcastTag);
            //_infoListener.SetMessageCallback(_capsuleInfoBroadcastTag);            
            _distanceListener = IGC.UnicastListener;
            //_distanceListener.SetMessageCallback(_capsuleDistanceBroadcastTag);
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {

        }

        private void HandleMessages()
        {
            while (_infoListener.HasPendingMessage)
            {
                MyIGCMessage message = _infoListener.AcceptMessage();
                if (message.Tag == _capsuleInfoBroadcastTag)
                {
                    if (message.Data is MatrixD)
                    {
                        IGC.SendUnicastMessage(message.Source, _unicastInfoTag, MyTuple.Create(_topDockMatrix, _bottomDockMatrix));
                    }
                }
            }

            while (_distanceListener.HasPendingMessage)
            {
                MyIGCMessage message = _infoListener.AcceptMessage();
                if (message.Tag == _capsuleInfoBroadcastTag)
                {
                    if (message.Data is MatrixD)
                    {
                        IGC.SendUnicastMessage(message.Source, _unicastInfoTag, MyTuple.Create(_topDockMatrix, _bottomDockMatrix));
                    }
                }
            }
        }
    }
}
