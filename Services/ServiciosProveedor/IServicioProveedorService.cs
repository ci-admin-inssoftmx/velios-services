using velios.Api.Models.ServiciosProveedor;

namespace velios.Api.Services.ServiciosProveedor
{
    public interface IServicioProveedorService
    {
        Task<AsignarServiciosResultado> AsignarServiciosAsync(AsignarServiciosProveedorRequest request);
        Task<IEnumerable<ServicioProveedorModel>> GetServiciosByProveedorAsync(int proveedorId);
        Task<IEnumerable<ProveedorPorServicioModel>> GetProveedoresByServicioAsync(int servicioId);

    }
}