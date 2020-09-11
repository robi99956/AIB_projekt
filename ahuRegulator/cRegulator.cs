using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ahuKlasy;

namespace ahuRegulator
{

    #region ParametryLokalne

    // przykładowa realizacja - do modyfikacji przez studenta
    class PI_reg_t
    {
        public double calka = 0;

        public double kp = 1;
        public double ki = 0;
        private double wy = 0;
        private double ts = 0;
        const double CORRECTION_WINDOW = 150;
        const double CORRECTION_SKEW = 2;

        private double windup_correction()
        {
            if( Math.Abs(wy) > CORRECTION_WINDOW )
            {
                if( wy < 0 ) return (wy+CORRECTION_WINDOW)*CORRECTION_SKEW; 
                if( wy > 0 ) return (wy-CORRECTION_WINDOW)*CORRECTION_SKEW;
            }

            return 0;
        }

        public PI_reg_t( double ts )
        {
            this.ts = ts;
        }

        public double wyjscie( double uchyb )
        {
            calka = calka + (uchyb - windup_correction()) * ts;
            return (wy = (kp * uchyb + ki * calka/60));
        }
    }

    #endregion

    /// <summary>
    /// przykładowe stany pracy centrali - do zmiany podkądem właściwego projektu
    /// </summary>
    public enum program_state_t
    {
        STOP = 0,
        RUN = 1,
        STARTUP = 2,
        COOLDOWN = 3,
        ALERT = 4
    }

    public class cRegulator
    {

        // ******** tych zmiennych nie ruszamy - są wykorzystywane przez program wywołujący
        public cDaneWeWy DaneWejsciowe = null;
        public cDaneWeWy DaneWyjsciowe = null;
        public double Ts = 1; //czas, co jaki jest wywoływana procedura regulatora


        // ********* zmienne definiowane przez studenta

        PI_reg_t PI_reg;

        program_state_t program_state = program_state_t.STOP;

        double CzasOdStartu = 0;
        double CzasOdStopu = 0;
        double OpoznienieZalaczeniaNagrzewnicy_s = 10;
        double OpoznienieWylaczeniaWentylatora_s = 15;

        // ***************************************************

        // Konstruktor
        public cRegulator()
        {
            PI_reg = new PI_reg_t(Ts);
        }

        double saturate_heater( double u )
        {
            if( u < 0 ) return 0;
            if( u > 100 ) return 100;

            return u;
        }

        void cascade_reg( in double u, out double u1, out double u2 )
        {
            u1 = saturate_heater( u );
            u2 = saturate_heater( u-100 );
        }

        /// <summary>
        /// funkcja wywoływana przez zewnętrzny program co czas Ts
        /// </summary>
        /// <returns></returns>
        public int iWywolanie()
        {
            // wnętrze funkcji dowolnie zmieniane przez studenta


            // przykład odczytu danych wejściowych
            double t_zad = DaneWejsciowe.Czytaj(eZmienne.TempZadana_C);
            double t_pom = DaneWejsciowe.Czytaj(eZmienne.TempPomieszczenia_C);
            bool boStart = DaneWejsciowe.Czytaj(eZmienne.PracaCentrali) > 0;

            // algorytm sterowania
            double heater_1 = 0, heater_2 = 0;
            bool boPracaWentylatoraNawiewu = false;

            switch (program_state)
            {
                case program_state_t.STOP:
                    {
                        heater_1 = heater_2 = 0;
                        boPracaWentylatoraNawiewu = false;
                        if(boStart)
                        {
                            program_state = program_state_t.STARTUP;
                        }
                        break;
                    }
                case program_state_t.STARTUP:
                    {
                        boPracaWentylatoraNawiewu = true;

                        if (CzasOdStartu < OpoznienieZalaczeniaNagrzewnicy_s)
                        {
                            heater_1 = heater_2 = 0;
                            CzasOdStartu += Ts;
                        }
                        else
                        {
                            program_state = program_state_t.RUN;
                            cascade_reg( PI_reg.wyjscie(t_zad-t_pom), out heater_1, out heater_2 );
                        }


                        break;
                    }
                case program_state_t.RUN:
                    {
                        boPracaWentylatoraNawiewu = true;

                        if (!boStart)
                        {
                            heater_1 = heater_2 = 0;
                            program_state = program_state_t.COOLDOWN;
                        }
                        else
                        {
                            cascade_reg( PI_reg.wyjscie(t_zad-t_pom), out heater_1, out heater_2 );
                        }
                        
                        break;
                    }
                case program_state_t.COOLDOWN:
                    {
                        if (CzasOdStopu < OpoznienieWylaczeniaWentylatora_s)
                        {
                            CzasOdStopu += Ts;
                            heater_1 = heater_2 = 0;
                            boPracaWentylatoraNawiewu = true;
                        }
                        else
                        {
                            program_state = program_state_t.STOP;
                            heater_1 = heater_2 = 0;
                            boPracaWentylatoraNawiewu = true;
                        }

                        break;
                    }
            }

            // ustawienie wyjść
            DaneWyjsciowe.Zapisz(eZmienne.WysterowanieNagrzewnicy1_pr, heater_1);
            DaneWyjsciowe.Zapisz(eZmienne.WysterowanieNagrzewnicy2_pr, heater_2);
            //DaneWejsciowe.Zapisz(eZmienne.ZalaczeniePompyNagrzewnicyWodnej1, heater_1 != 0);
            //DaneWejsciowe.Zapisz(eZmienne.ZalaczeniePompyNagrzewnicyWodnej2, heater_2 != 0);
            DaneWyjsciowe.Zapisz(eZmienne.ZezwolenieNaPraceWentylatoraNawiewu, boPracaWentylatoraNawiewu);

            return 0;
        }

        /// <summary>
        /// wywołanie formularza z parametrami
        /// </summary>
        public void ZmienParametry()
        {
            // wnętrze funkcji dowolnie zmieniane przez studenta
            fmParametry fm = new fmParametry();
            fm.kp = PI_reg.kp;
            fm.ki = PI_reg.ki;

            fm.t1 = OpoznienieZalaczeniaNagrzewnicy_s;
            fm.t2 = OpoznienieWylaczeniaWentylatora_s;

            if (fm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PI_reg.kp = fm.kp;
                PI_reg.ki = fm.ki;

                OpoznienieZalaczeniaNagrzewnicy_s = fm.t1;
                OpoznienieWylaczeniaWentylatora_s = fm.t2;
            }
        }
    }
}
