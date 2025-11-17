using blazor.Components.Data;
using FacturaModel = blazor.Components.Data.Factura;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace blazor.Components.Servicios
{
    public enum CriterioOrdenacion
    {
        FechaDescendente,
        IdDescendente,
        NombreClienteAscendente
    }

    public class ServicioControlador
    {
        private readonly ServicioFacturas _servicioFacturas;

        private const string CLAVE_FILTRO_CLIENTE = "FiltroNombreCliente";
        private const string CLAVE_ORDENACION = "CriterioOrdenacion";

        public string FiltroNombreCliente { get; set; } = string.Empty;
            
        public CriterioOrdenacion OrdenacionSeleccionada { get; set; } = CriterioOrdenacion.FechaDescendente;

        // Usar el tipo fully-qualified para evitar ambigüedades con otros namespaces llamados "Factura"
        private List<blazor.Components.Data.Factura> _facturasEnMemoria = new List<blazor.Components.Data.Factura>();

        public ServicioControlador(ServicioFacturas servicioFacturas)
        {
            _servicioFacturas = servicioFacturas;
        }

        public async Task CargarFiltroAsync()
        {
            FiltroNombreCliente = await _servicioFacturas.ObtenerConfiguracionAsync(CLAVE_FILTRO_CLIENTE);

            var ordenacionGuardada = await _servicioFacturas.ObtenerConfiguracionAsync(CLAVE_ORDENACION);
            if (Enum.TryParse(ordenacionGuardada, out CriterioOrdenacion orden))
            {
                OrdenacionSeleccionada = orden;
            }
        }

        public async Task CargarFacturasAsync()
        {
            _facturasEnMemoria = (await _servicioFacturas.ObtenerTodasAsync()).ToList();
        }

        public IEnumerable<blazor.Components.Data.Factura> ObtenerFacturasFiltradas()
        {
            IEnumerable<blazor.Components.Data.Factura> resultado = _facturasEnMemoria;

            if (!string.IsNullOrWhiteSpace(FiltroNombreCliente))
            {
                resultado = resultado.Where(f =>
                    f.NombreCliente.Contains(FiltroNombreCliente, StringComparison.OrdinalIgnoreCase));
            }

            resultado = OrdenacionSeleccionada switch
            {
                CriterioOrdenacion.IdDescendente => resultado.OrderByDescending(f => f.Id),
                CriterioOrdenacion.NombreClienteAscendente => resultado.OrderBy(f => f.NombreCliente),
                CriterioOrdenacion.FechaDescendente or _ => resultado.OrderByDescending(f => f.FechaFactura),
            };

            Task.Run(async () =>
            {
                await _servicioFacturas.GuardarConfiguracionAsync(CLAVE_FILTRO_CLIENTE, FiltroNombreCliente);
                await _servicioFacturas.GuardarConfiguracionAsync(CLAVE_ORDENACION, OrdenacionSeleccionada.ToString());
            }).FireAndForget();

            return resultado;
        }

        public async Task GuardarNuevaFacturaAsync(blazor.Components.Data.Factura nuevaFactura)
        {
            await _servicioFacturas.AgregarFacturaAsync(nuevaFactura);
        }

        public async Task ActualizarFacturaAsync(blazor.Components.Data.Factura facturaEditada)
        {
            await _servicioFacturas.ActualizarFacturaAsync(facturaEditada);

            var index = _facturasEnMemoria.FindIndex(f => f.Id == facturaEditada.Id);
            if (index != -1)
            {
                _facturasEnMemoria[index] = facturaEditada;
            }
        }

        public async Task EliminarFacturaAsync(int facturaId)
        {
            await _servicioFacturas.EliminarFacturaAsync(facturaId);
            var facturaAEliminar = _facturasEnMemoria.FirstOrDefault(f => f.Id == facturaId);
            if (facturaAEliminar != null)
            {
                _facturasEnMemoria.Remove(facturaAEliminar);
            }
        }

        public async Task<blazor.Components.Data.Factura?> ObtenerFacturaPorIdAsync(int id)
        {
            var facturaEnMemoria = _facturasEnMemoria.FirstOrDefault(f => f.Id == id);
            if (facturaEnMemoria != null)
            {
                return facturaEnMemoria;
            }

            var facturaDesdeDB = await _servicioFacturas.ObtenerPorIdAsync(id);

            if (facturaDesdeDB != null)
            {
                _facturasEnMemoria.Add(facturaDesdeDB);
            }

            return facturaDesdeDB;
        }
    }

    public static class TaskExtension
    {
        public static void FireAndForget(this Task task)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en tarea FireAndForget: {ex.Message}");
                }
            });
        }
    }
}