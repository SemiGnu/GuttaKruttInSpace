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
        MyIni _ini = new MyIni();
        IMyTextSurface _lcd;
        List<Hangar> _hangars;
        IMyBroadcastListener _listener;

        string _broadcastTag = "HANGAR_LISTENER";

        public Program()
        {
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
            {
                Echo($"CustomData error:\nLine {result}");
            }
            var savedHangars = Storage.Split(new[] { ';' },StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split(',')).ToDictionary(h => h[0], h => h[1]);
            _hangars = _ini.Get("Hangars", "Names").ToString()
                .Split(',')
                .Select(name => {
                    string status;
                    return new Hangar(name, GridTerminalSystem, savedHangars.TryGetValue(name, out status) ? status : "Closed");
                })
                .ToList();
            _lcd = Me.GetSurface(0);
            _listener = IGC.RegisterBroadcastListener(_broadcastTag);
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {
            Storage = string.Join(";",_hangars.Select(h => $"{h.Name},{h.Status}"));
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0)
            {
                _hangars.FirstOrDefault(h => h.Name == argument)?.Toggle();
            }
            if ((updateSource & UpdateType.Update10) > 0)
            {
                HandleMessages();
                _hangars.ForEach(h => h.Main());
                var status = $"Airlock status:\n{string.Join("\n", _hangars.Select(h => $"{h.Name} - {h.Status}"))}";
                _lcd.WriteText(status);
            }
        }

        private void HandleMessages()
        {
            while (_listener.HasPendingMessage)
            {
                MyIGCMessage message = _listener.AcceptMessage();
                if (message.Tag == _broadcastTag)
                {
                    if (message.Data is MatrixD)
                    {
                        var closestHangar = GetClosestHangar((MatrixD)message.Data);
                        closestHangar.Toggle();
                    }
                }
            }
        }

        private Hangar GetClosestHangar(MatrixD data)
        {
            return _hangars.OrderBy(h => Vector3.Distance(data.Translation, h.Translation)).FirstOrDefault();
        }

        public class Hangar
        {
            List<IMyDoor> _hangarSideDoors = new List<IMyDoor>();
            List<IMyAirVent> _hangarAirVents = new List<IMyAirVent>();
            List<IMyTextSurface> _hangarLcds = new List<IMyTextSurface>();
            List<IMyLightingBlock> _hangarLights = new List<IMyLightingBlock>();
            List<IMyAirtightHangarDoor> _hangarDoors = new List<IMyAirtightHangarDoor>();

            public string Name { get; set; }
            public Vector3 Translation { get; private set; }
            public enum HangarStatus
            {
                Opening, Open, Closing, Closed
            }
            DoorStatus _doorStatus => _hangarDoors.First().Status;
            VentStatus _ventStatus => _hangarAirVents.First().GetOxygenLevel() == 0 ? VentStatus.Depressurized : _hangarAirVents.First().Status;
            public HangarStatus Status { get; private set; }
            bool ShouldDepressurize => Status == HangarStatus.Opening && _ventStatus == VentStatus.Pressurized;
            bool ShouldPressurize => Status == HangarStatus.Closing && _doorStatus == DoorStatus.Closed;
            bool ShouldOpenDoors => Status == HangarStatus.Opening && _ventStatus == VentStatus.Depressurized;
            bool ShouldCloseDoors => Status == HangarStatus.Closing && _doorStatus == DoorStatus.Open;
            bool HasOpened => Status == HangarStatus.Opening && _doorStatus == DoorStatus.Open;
            bool HasClosed => Status == HangarStatus.Closing && _ventStatus == VentStatus.Pressurized;
            bool IsIdle => Status == HangarStatus.Closed || Status == HangarStatus.Open;

            public Hangar(string name, IMyGridTerminalSystem grid, string status)
            {
                Name = name;
                Status = (HangarStatus) Enum.Parse(typeof(HangarStatus),status);
                var group = grid.GetBlockGroupWithName($"{name} Hangar Control");
                group.GetBlocksOfType(_hangarDoors);
                group.GetBlocksOfType(_hangarAirVents);
                group.GetBlocksOfType(_hangarSideDoors, d => !(d is IMyAirtightHangarDoor));
                group.GetBlocksOfType(_hangarLcds);
                group.GetBlocksOfType(_hangarLights);
                Translation = _hangarDoors.First().WorldMatrix.Translation;
            }

            public void Main()
            {
                if (!IsIdle)
                {
                    UpdateState();
                }

                _hangarLights.ForEach(l => l.Enabled = !IsIdle);
                var status = "test";
                if (Status == HangarStatus.Closed || Status == HangarStatus.Open) status = $"{Status}";
                else if (_ventStatus == VentStatus.Depressurizing || _ventStatus == VentStatus.Pressurizing) status = $"{_ventStatus} {_hangarAirVents.First().GetOxygenLevel():p0}";
                else status = $"{_doorStatus} {_hangarDoors.First().OpenRatio:p0}";
                _hangarLcds.ForEach(l => l.WriteText(status));
            }

            public void Toggle()
            {
                if (Status == HangarStatus.Open || Status == HangarStatus.Opening)
                {
                    if (_doorStatus == DoorStatus.Open || _doorStatus == DoorStatus.Opening)
                    {
                        _hangarDoors.ForEach(d => d.CloseDoor());
                    }
                    if (_doorStatus == DoorStatus.Closed && (_ventStatus == VentStatus.Depressurized || _ventStatus == VentStatus.Depressurizing))
                    {
                        Pressurize();
                    }
                    Status = HangarStatus.Closing;
                }
                else
                {
                    if (_ventStatus == VentStatus.Depressurized && (_doorStatus == DoorStatus.Closed || _doorStatus == DoorStatus.Closing))
                    {
                        _hangarDoors.ForEach(d => d.OpenDoor());
                    }
                    if ((_ventStatus == VentStatus.Pressurized || _ventStatus == VentStatus.Pressurizing))
                    {
                        Depressurize();
                    }
                    Status = HangarStatus.Opening;
                }
            }

            void UpdateState()
            {
                if (ShouldDepressurize) Depressurize();
                if (ShouldPressurize) Pressurize();
                if (ShouldOpenDoors) _hangarDoors.ForEach(a => a.OpenDoor());
                if (ShouldCloseDoors) _hangarDoors.ForEach(a => a.CloseDoor());
                if (HasClosed) Status = HangarStatus.Closed;
                if (HasOpened) Status = HangarStatus.Open;
                LockSideDoors();
            }

            private void Depressurize()
            {
                _hangarAirVents.ForEach(a => a.Depressurize = true);
                _hangarSideDoors.ForEach(s =>
                {
                    s.Enabled = true;
                    s.CloseDoor();
                });
            }

            private void Pressurize()
            {
                _hangarAirVents.ForEach(a => a.Depressurize = false);
                _hangarSideDoors.ForEach(s =>
                {
                    s.Enabled = true;
                    s.OpenDoor();
                });
            }

            private void LockSideDoors()
            {
                _hangarSideDoors.ForEach(s => s.Enabled = _ventStatus != VentStatus.Depressurized && s.Status != DoorStatus.Closed);
            }


        }
    }
}
