using System.Collections.Generic;
using CateringService.Models;

namespace CateringService.Services
{
    public interface IVehicleRegistry
    {
        (string? VehicleId, string? BaseNode, string? Destination) AcquireAvailableVehicle(string aircraftId);
        void ReleaseVehicle(string vehicleId, string baseNode);
        void AddVehicle(string vehicleId, string baseNode, Dictionary<string, string> serviceSpots);
        bool CanRegisterNewVehicle();
        bool TryAddVehicle(string vehicleId, string baseNode, Dictionary<string, string> serviceSpots);
        void MarkAsBusy(string vehicleId);
        void MarkAsAvailable(string vehicleId, string baseNode);

        void UpdateCurrentNode(string vehicleId, string currentNode);
        IEnumerable<VehicleStatusInfo> GetAllVehicles();
        void Reset();
    }
}
