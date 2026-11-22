using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using velios.Api.Models.ReporteMaterialidad;
using velios.Api.Models.Tareas;

namespace velios.Api.Services;

/// <summary>Contenido de la página 2 (Seguimiento de ruta), compartido por el reporte preliminar y el final.</summary>
public static class PaginaMapaRutaReporte
{
    private static string Truncar(string? texto, int max)
    {
        if (string.IsNullOrWhiteSpace(texto)) return texto ?? string.Empty;
        return texto.Length <= max ? texto : texto.Substring(0, max).TrimEnd() + "...";
    }

    public static void Contenido(
        IContainer container,
        MapaRutaReporteDto mapa,
        TareaReporteDto tarea,
        string direccionDisplay,
        byte[]? documentoIconBytes,
        byte[]? carpetaIconBytes)
    {
        var fin = mapa.Finalizada && mapa.FechaFin.HasValue ? mapa.FechaFin.Value : DateTime.Now;
        var duracion = fin - mapa.FechaInicio;
        if (duracion < TimeSpan.Zero) duracion = TimeSpan.Zero;
        var duracionTxt = duracion.TotalHours >= 1
            ? $"{(int)duracion.TotalHours}h {duracion.Minutes}m"
            : $"{duracion.Minutes}m";

        string Transcurrido(DateTime hora)
        {
            var t = hora - mapa.FechaInicio;
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return t.TotalHours >= 1 ? $"+{(int)t.TotalHours}h {t.Minutes:00}m" : $"+{(int)t.TotalMinutes} min";
        }

        var colorBorde = mapa.Finalizada ? "#22C55E" : "#F15A24";
        var colorTexto = mapa.Finalizada ? "#15803D" : "#C2410C";

        container.PaddingLeft(28).ExtendVertical().Row(row =>
        {
            // ================= COLUMNA IZQUIERDA: título + mapa grande =================
            row.RelativeItem(2.7f).PaddingRight(10).Column(left =>
            {
                left.Spacing(10);

                left.Item().Row(r =>
                {
                    r.ConstantItem(34).Height(34).Border(1).BorderColor("#D1D5DB")
                        .Background("#F9FAFB").AlignCenter().AlignMiddle()
                        .Element(icon =>
                        {
                            if (documentoIconBytes is not null && documentoIconBytes.Length > 0)
                                icon.Padding(6).Image(documentoIconBytes, ImageScaling.FitArea);
                            else
                                icon.Text("≡").FontSize(18).Bold().FontColor("#24364D");
                        });
                    r.ConstantItem(10);
                    r.RelativeItem().AlignMiddle().Text("Detalle de Ruta")
                        .SemiBold().FontSize(11).FontColor("#24364D");
                });

                left.Item().Border(1).BorderColor("#D6DCE5").Background(Colors.White)
                    .CornerRadius(8).Padding(2)
                    .Image(mapa.Imagen, ImageScaling.FitWidth);

                if (!mapa.TieneMapaBase)
                    left.Item().Text("Mapa base no disponible; se muestra solo el recorrido.")
                        .FontSize(7.5f).FontColor("#6B7280");
            });

            // ================= SIDEBAR DERECHO (estilo web Velios) =================
            row.ConstantItem(175).ExtendVertical().Background("#F3F4F6").CornerRadius(8).Padding(8).Layers(layers =>
            {
                layers.PrimaryLayer().ScaleToFit().Column(right =>
                {
                    right.Spacing(8);

                    // ---- Simbología en tarjeta ----
                    right.Item().Background(Colors.White).Border(1).BorderColor("#E5E7EB").CornerRadius(8)
                        .PaddingVertical(6).PaddingHorizontal(4).Row(r =>
                        {
                            void Leyenda(Action<IContainer> icono, string texto)
                            {
                                r.RelativeItem().Column(c =>
                                {
                                    c.Item().Height(16).AlignCenter().AlignMiddle().Element(icono);
                                    c.Item().PaddingTop(3).AlignCenter()
                                        .Text(texto).FontSize(6).FontColor("#667489").AlignCenter();
                                });
                            }

                            Leyenda(el => el.Width(12).Height(12).Background(Colors.White).Border(2.5f)
                                .BorderColor("#111827").CornerRadius(6), "Inicio de ruta");
                            Leyenda(el => el.Width(12).Height(12).Border(2.5f).BorderColor("#111827")
                                .CornerRadius(3).Padding(2.5f).Background("#111827"), "Fin de ruta");
                            Leyenda(el => el.Width(16).Height(2.5f).Background("#111827"), "Ruta real");
                            Leyenda(el => el.Width(16).Height(2.5f).Background("#7C3AED"), "Ruta planeada");
                            Leyenda(el => el.Width(14).Height(14).Background("#F26A1B").CornerRadius(4).Padding(2.5f)
                                .Image(IconosReporte.Camara("#FFFFFF"), ImageScaling.FitArea), "Evidencia");
                        });

                    // ---- Sucursal en tarjeta ----
                    right.Item().Background(Colors.White).Border(1).BorderColor("#E5E7EB").CornerRadius(8)
                        .Padding(8).Row(r =>
                        {
                            r.ConstantItem(14).AlignMiddle().Height(18)
                                .Image(IconosReporte.Pin("#F26A1B"), ImageScaling.FitArea);
                            r.ConstantItem(6);
                            r.RelativeItem().Column(c =>
                            {
                                var sucursal = ReporteLayout.Ajustar(tarea.NombreCentroTrabajo ?? "Sin centro de trabajo", 50, 11, 8, 18);
                                c.Item().Text(sucursal.Texto).Bold().FontSize(sucursal.Size).FontColor("#24364D");
                                c.Item().Text("Sucursal / CT").SemiBold().FontSize(7.5f).FontColor("#F26A1B");
                                c.Item().PaddingTop(2).Text(Truncar(direccionDisplay, 70))
                                    .FontSize(7).FontColor("#667489");
                            });
                        });

                    // ---- Línea de tiempo ----
                    right.Item().PaddingHorizontal(2).Column(tl =>
                    {
                        void Hito(Action<IContainer> punto, string fecha, string etiqueta, bool conLinea)
                        {
                            tl.Item().Row(r =>
                            {
                                r.ConstantItem(22).Column(c =>
                                {
                                    c.Item().AlignCenter().Element(punto);
                                    if (conLinea)
                                        c.Item().AlignCenter().Element(line => line.Width(2).Height(18).Background("#D1D5DB"));
                                });
                                r.ConstantItem(4);
                                r.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(fecha).Bold().FontSize(9.5f).FontColor("#24364D");
                                    c.Item().Text(etiqueta).FontSize(7.5f).FontColor("#667489");
                                });
                            });
                        }

                        Hito(el => el.Width(14).Height(14).Background(Colors.White).Border(3).BorderColor("#111827").CornerRadius(7),
                            $"{mapa.FechaInicio:dd/MM/yyyy HH:mm}", "Inicio de ruta", true);

                        Hito(el =>
                        {
                            if (mapa.Finalizada)
                                el.Width(14).Height(14).Background("#22C55E").CornerRadius(7).Padding(3)
                                    .Image(IconosReporte.Check("#FFFFFF"), ImageScaling.FitArea);
                            else
                                el.Width(14).Height(14).Background(Colors.White).Border(2).BorderColor("#94A3B8").CornerRadius(7);
                        },
                            mapa.Finalizada && mapa.FechaFin.HasValue ? $"{mapa.FechaFin.Value:dd/MM/yyyy HH:mm}" : "En curso",
                            "Fin de ruta", false);
                    });

                    // ---- Recuadro oscuro ----
                    right.Item().Background("#1B2B44").CornerRadius(10).PaddingVertical(10).PaddingHorizontal(10).Column(c =>
                    {
                        c.Item().AlignCenter().Text("TOTAL EVIDENCIAS").FontSize(7.5f).Bold()
                            .LetterSpacing(0.08f).FontColor("#CBD5E1");
                        c.Item().AlignCenter().Text(mapa.TotalEvidencias.ToString()).Bold().FontSize(20).FontColor("#F5A524");
                        c.Item().PaddingVertical(5).LineHorizontal(0.5f).LineColor("#475569");
                        c.Item().AlignCenter().Text(mapa.Finalizada ? "DURACIÓN TOTAL" : "DURACIÓN ACTUAL")
                            .FontSize(7.5f).Bold().LetterSpacing(0.08f).FontColor("#CBD5E1");
                        c.Item().AlignCenter().Text(duracionTxt).Bold().FontSize(15).FontColor(Colors.White);
                    });

                    // ---- Evidencias por punto ----
                    const int maxPuntos = 8;
                    var puntos = mapa.Puntos.Take(maxPuntos).ToList();
                    if (puntos.Count > 0)
                    {
                        right.Item().Row(r =>
                        {
                            r.RelativeItem().Text("EVIDENCIAS POR PUNTO").FontSize(6.5f).SemiBold()
                                .LetterSpacing(0.06f).FontColor("#667489");
                            r.AutoItem().Text($"{mapa.Puntos.Count} puntos · {mapa.FechaInicio:dd/MM/yyyy}")
                                .FontSize(6.5f).FontColor("#667489");
                        });

                        right.Item().Background(Colors.White).Border(1).BorderColor("#E5E7EB").CornerRadius(8)
                            .PaddingHorizontal(5).PaddingVertical(2).Column(lista =>
                            {
                                for (var i = 0; i < puntos.Count; i++)
                                {
                                    var p = puntos[i];

                                    if (i > 0)
                                        lista.Item().LineHorizontal(0.5f).LineColor("#EEF0F3");

                                    lista.Item().PaddingVertical(4).Row(r =>
                                    {
                                        r.ConstantItem(18).Height(18).Background("#F26A1B").CornerRadius(9)
                                            .AlignCenter().AlignMiddle()
                                            .Text(p.Cantidad.ToString()).Bold().FontSize(8).FontColor(Colors.White);
                                        r.ConstantItem(5);

                                        r.RelativeItem().Column(c =>
                                        {
                                            c.Item().Text($"Punto {p.Orden}").Bold().FontSize(8.5f).FontColor("#24364D");
                                            c.Item().Row(inf =>
                                            {
                                                inf.AutoItem().AlignMiddle().Width(7).Height(7)
                                                    .Image(IconosReporte.Camara("#F26A1B"), ImageScaling.FitArea);
                                                inf.ConstantItem(2);
                                                inf.AutoItem().Text(p.Cantidad == 1 ? "1 evidencia" : $"{p.Cantidad} evidencias")
                                                    .FontSize(6.5f).SemiBold().FontColor("#F26A1B");
                                                inf.ConstantItem(3);
                                                inf.AutoItem().Text("·").FontSize(6.5f).FontColor("#667489");
                                                inf.ConstantItem(3);
                                                inf.AutoItem().AlignMiddle().Width(7).Height(7)
                                                    .Image(IconosReporte.Reloj("#667489"), ImageScaling.FitArea);
                                                inf.ConstantItem(2);
                                                inf.AutoItem().Text($"{p.Hora:HH:mm}").FontSize(6.5f).FontColor("#667489");
                                            });
                                        });

                                        r.ConstantItem(3);
                                        r.AutoItem().AlignMiddle().Background("#EEF0F3").CornerRadius(8)
                                            .PaddingHorizontal(4).PaddingVertical(2)
                                            .Text(Transcurrido(p.Hora)).FontSize(6.5f).SemiBold().FontColor("#667489");
                                    });
                                }
                            });

                        if (mapa.Puntos.Count > maxPuntos)
                            right.Item().Text($"+ {mapa.Puntos.Count - maxPuntos} puntos más")
                                .FontSize(7).FontColor("#667489");
                    }

                    right.Item().Height(46);
                });

                // ---- Botón de estado anclado al fondo ----
                layers.Layer().AlignBottom()
                    .Border(2).BorderColor(colorBorde).Background(Colors.White).CornerRadius(10)
                    .PaddingVertical(11).AlignCenter()
                    .Text(mapa.Finalizada ? "RUTA FINALIZADA" : "RUTA EN CURSO")
                    .Bold().FontSize(9.5f).LetterSpacing(0.08f).FontColor(colorTexto);
            });
        });
    }
}