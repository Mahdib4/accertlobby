namespace Accertone.CarSetup
{
    /// <summary>
    /// Interface implemented by vehicle physics controllers, engine models,
    /// and aerodynamics simulators to receive tuned F1CarSetupData.
    /// </summary>
    public interface IVehicleSetupReceiver
    {
        void ApplyCarSetup(F1CarSetupData setup);
    }
}
