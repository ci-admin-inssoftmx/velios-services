using velios.Api.Models.PresupuestoGuardado;

namespace velios.Api.Services.PresupuestoGuardado
{
    public interface IPresupuestoGuardadoService
    {
        Task<int> GuardarPresupuestoAsync(GuardarPresupuestoRequest request);
        Task<IEnumerable<PresupuestoGuardadoModel>> GetPresupuestosByTareaAsync(int tareaId);
    }
}