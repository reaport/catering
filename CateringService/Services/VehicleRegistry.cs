﻿using System.Collections.Generic;
using System.Linq;
using CateringService.Models;
using Microsoft.Extensions.Logging;

namespace CateringService.Services
{
    public class VehicleRegistry : IVehicleRegistry
    {
        private readonly Dictionary<string, string> _vehicleStatus = new Dictionary<string, string>();
        private readonly Dictionary<string, Dictionary<string, string>> _serviceMapping = new Dictionary<string, Dictionary<string, string>>();
        private readonly object _syncLock = new object();
        private readonly ILogger<VehicleRegistry> _logger;

        public VehicleRegistry(ILogger<VehicleRegistry> logger)
        {
            _logger = logger;
        }

        public (string? VehicleId, string? BaseNode, string? Destination) AcquireAvailableVehicle(string flightId)
        {
            lock (_syncLock)
            {
                var entry = _vehicleStatus.FirstOrDefault(kvp => kvp.Value != "in use");
                if (!string.IsNullOrEmpty(entry.Key))
                {
                    string vehicleId = entry.Key;
                    string baseNode = entry.Value;
                    string? destination = null;
                    // Если для данного рейса задано специальное назначение, используем его
                    if (_serviceMapping.ContainsKey(vehicleId) && _serviceMapping[vehicleId].ContainsKey(flightId))
                    {
                        destination = _serviceMapping[vehicleId][flightId];
                    }
                    _vehicleStatus[vehicleId] = "in use";
                    _logger.LogInformation("AcquireAvailableVehicle: using {VehicleId} for flight {FlightId}", vehicleId, flightId);
                    return (vehicleId, baseNode, destination);
                }
            }
            _logger.LogWarning("AcquireAvailableVehicle: no free vehicle for flight {FlightId}", flightId);
            return (null, null, null);
        }

        public void ReleaseVehicle(string vehicleId, string baseNode)
        {
            lock (_syncLock)
            {
                if (_vehicleStatus.ContainsKey(vehicleId))
                {
                    _vehicleStatus[vehicleId] = baseNode;
                    _logger.LogInformation("ReleaseVehicle: vehicle {VehicleId} returned to base {BaseNode}", vehicleId, baseNode);
                }
            }
        }

        public void AddVehicle(string vehicleId, string baseNode, Dictionary<string, string> serviceSpots)
        {
            lock (_syncLock)
            {
                // Глобальный лимит: не более 5 машин
                if (_vehicleStatus.Count < 5)
                {
                    _vehicleStatus[vehicleId] = baseNode;
                    _serviceMapping[vehicleId] = serviceSpots;
                    _logger.LogInformation("AddVehicle: {VehicleId} at base {BaseNode}", vehicleId, baseNode);
                }
                else
                {
                    _logger.LogWarning("Global vehicle limit reached. Cannot add vehicle {VehicleId}", vehicleId);
                }
            }
        }

        public bool CanRegisterNewVehicle()
        {
            lock (_syncLock)
            {
                return _vehicleStatus.Count < 5;
            }
        }

        public bool TryAddVehicle(string vehicleId, string baseNode, Dictionary<string, string> serviceSpots)
        {
            lock (_syncLock)
            {
                if (_vehicleStatus.Count < 5)
                {
                    _vehicleStatus[vehicleId] = baseNode;
                    _serviceMapping[vehicleId] = serviceSpots;
                    _logger.LogInformation("TryAddVehicle: Vehicle {VehicleId} added.", vehicleId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("TryAddVehicle: Global vehicle limit reached. Cannot add vehicle {VehicleId}.", vehicleId);
                    return false;
                }
            }
        }

        public void MarkAsBusy(string vehicleId)
        {
            lock (_syncLock)
            {
                if (_vehicleStatus.ContainsKey(vehicleId))
                {
                    _vehicleStatus[vehicleId] = "in use";
                    _logger.LogInformation("MarkAsBusy: vehicle {VehicleId} marked as Busy", vehicleId);
                }
            }
        }

        public void MarkAsAvailable(string vehicleId, string baseNode)
        {
            lock (_syncLock)
            {
                if (_vehicleStatus.ContainsKey(vehicleId))
                {
                    _vehicleStatus[vehicleId] = baseNode;
                    _logger.LogInformation("MarkAsAvailable: vehicle {VehicleId} marked as Available", vehicleId);
                }
            }
        }

        public IEnumerable<VehicleStatusInfo> GetAllVehicles()
        {
            lock (_syncLock)
            {
                var list = new List<VehicleStatusInfo>();
                foreach (var kvp in _vehicleStatus)
                {
                    string vehicleId = kvp.Key;
                    bool isBusy = kvp.Value == "in use";
                    string baseNode = isBusy ? "N/A" : kvp.Value;
                    Dictionary<string, string> serviceSpots = _serviceMapping.ContainsKey(vehicleId)
                        ? _serviceMapping[vehicleId]
                        : new Dictionary<string, string>();
                    list.Add(new VehicleStatusInfo
                    {
                        VehicleId = vehicleId,
                        BaseNode = baseNode,
                        Status = isBusy ? "Busy" : "Available",
                        ServiceSpots = serviceSpots
                    });
                }
                return list;
            }
        }
    }
}
