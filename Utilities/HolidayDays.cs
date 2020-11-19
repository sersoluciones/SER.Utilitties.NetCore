using SER.Utilitties.NetCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities
{
    public static class HolidayDays
    {
        public static List<Holidays> DiasFestivos(int Anio)
        {
            DateTime Pascua = calcularPascua(Anio);

            List<Holidays> diasFestivos = new List<Holidays>();

            IncluirFecha(ref diasFestivos, new DateTime(Anio, 1, 1), "Primero de Enero"); //Primero de Enero
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, new DateTime(Anio, 1, 6)), "Reyes magos"); //Reyes magos
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, new DateTime(Anio, 3, 19)), "San Jose"); //San Jose
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Sunday, Pascua, true, false), "Domingo de Ramos"); //Domingo de Ramos
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Thursday, Pascua, true), "Jueves Santo"); //Jueves Santo
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Friday, Pascua, true), "Viernes Santo"); //Viernes Santo
            IncluirFecha(ref diasFestivos, Pascua, "Pascua"); //Pascua
            IncluirFecha(ref diasFestivos, new DateTime(Anio, 5, 1), "Dia del trabajo"); //Dia del trabajo


            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, Pascua).AddDays(42), "Ascensión de Jesús"); //Ascensión de Jesús
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, Pascua).AddDays(63), "Corpus Christi"); //Corpus Christi
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, Pascua).AddDays(70), "Sagrado Corazón"); //Sagrado Corazón


            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, new DateTime(Anio, 6, 29)), "san Pedro y san Pablo"); //san Pedro y san Pablo
            IncluirFecha(ref diasFestivos, new DateTime(Anio, 7, 20), "Grito de Independencia"); //Grito de Independencia
            IncluirFecha(ref diasFestivos, new DateTime(Anio, 8, 7), "Batalla de Boyacá"); // Batalla de Boyacá
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, new DateTime(Anio, 8, 15)), "Asuncion de la virgen"); //Asuncion de la virgen
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, new DateTime(Anio, 10, 12)), "Día de la Raza"); //Día de la Raza
            //IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, new DateTime(Anio, 10, 12)), "Todos los Santos"); //Todos los Santos
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, new DateTime(Anio, 11, 1)), "Todos los Santos"); //Todos los Santos
            IncluirFecha(ref diasFestivos, SiguienteDiaSemana(DayOfWeek.Monday, new DateTime(Anio, 11, 11)), "Independencia de Cartagena"); //Independencia de Cartagena
            IncluirFecha(ref diasFestivos, new DateTime(Anio, 12, 8), "Inmaculada Concepción"); // Inmaculada Concepción
            IncluirFecha(ref diasFestivos, new DateTime(Anio, 12, 25), "Navidad"); // Navidad

            return diasFestivos;
        }

        private static void IncluirFecha(ref List<Holidays> ListaDias, DateTime fecha, String desc)
        {
            if (ListaDias.Select(x => x.Date).Contains(fecha) == false)
                ListaDias.Add(new Holidays()
                {
                    Date = fecha,
                    Description = desc
                });
        }

        private static DateTime SiguienteDiaSemana(DayOfWeek DiaSemana, DateTime fecha, bool haciaAtras = false, bool inclusive = true)
        {
            if (inclusive)
            {
                if (fecha.DayOfWeek == DiaSemana)
                {
                    return fecha;
                }
            }
            else
            {
                if (haciaAtras)
                    fecha = fecha.AddDays(-1);
                else
                    fecha = fecha.AddDays(1);
            }

            while (fecha.DayOfWeek != DiaSemana)
                if (haciaAtras)
                    fecha = fecha.AddDays(-1);
                else
                    fecha = fecha.AddDays(1);

            return fecha;
        }

        private static DateTime calcularPascua(int Anio)
        {

            int a, b, c, d, e;
            int m = 24, n = 5;


            if (Anio >= 1583 && Anio <= 1699)
            {
                m = 22;
                n = 2;
            }
            else if (Anio >= 1700 && Anio <= 1799)
            {
                m = 23;
                n = 3;
            }
            else if (Anio >= 1800 && Anio <= 1899)
            {
                m = 23;
                n = 4;
            }
            else if (Anio >= 1900 && Anio <= 2099)
            {
                m = 24;
                n = 5;
            }
            else if (Anio >= 2100 && Anio <= 2199)
            {
                m = 24;
                n = 6;
            }
            else if (Anio >= 2200 && Anio <= 2299)
            {
                m = 25;
                n = 0;
            }

            a = Anio % 19;
            b = Anio % 4;
            c = Anio % 7;
            d = ((a * 19) + m) % 30;
            e = ((2 * b) + (4 * c) + (6 * d) + n) % 7;


            int dia = d + e;


            if (dia < 10) //Marzo
                return new DateTime(Anio, 3, dia + 22);
            else //Abril
            {

                if (dia == 26)
                    dia = 19;
                else if (dia == 25 && d == 28 && e == 6 && a > 10)
                    dia = 18;
                else
                    dia -= 9;

                return new DateTime(Anio, 4, dia);
            }
        }
    }
}
