using Npgsql;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LakkTraceWPF
{
    /// <summary>
    /// Interaction logic for LacquerLoad.xaml
    /// </summary>
    public partial class LacquerLoad : Window
    {
        public object Keys { get; private set; }
        private double actualDateValue = 0;

        public LacquerLoad()
        {
            InitializeComponent();
            SettingUpTheParameters();
        }

        private void SettingUpTheParameters()
        {
            //Starting date for expiry
            expiryDate.SelectedDate = DateTime.Today;
            // Custom format for expiry date
            CultureInfo ci = CultureInfo.CreateSpecificCulture(CultureInfo.CurrentCulture.Name);
            ci.DateTimeFormat.ShortDatePattern = "MM-yyyy";
            Thread.CurrentThread.CurrentCulture = ci;
            // reset the "visible help" text
            isBatchValid.Text = "";
            isDateValid.Text = "";
            //give the focus for batch textbox
            batchTbx.Focus();
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // checks the inputs
        private bool dataValid()
        {
            if (batchTbx.Text.Length > 0 && expiryDate.SelectedDate > DateTime.Today)
                return true;
            else
                return false;
        }


        //calling the validation regex from the app.config
        public bool RegexValidation(string dataToValidate, string datafieldName)
        {
            string rgx = ConfigurationManager.AppSettings[datafieldName];
            return (Regex.IsMatch(dataToValidate, rgx));
        }

        // Syntax check of the product
        private void db_insert() //DB insert
        {
            try
            {
                string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
                // Making connection with Npgsql provider
                var conn = new NpgsqlConnection(connstring);
                conn.Open();
                // building SQL query
                var cmd = new NpgsqlCommand("INSERT INTO lakk (batch, expdate, timestamp, workstation) VALUES(:batch, :expdate, :timestamp, :workstation)", conn);
                cmd.Parameters.Add(new NpgsqlParameter("batch", batchTbx.Text));
                cmd.Parameters.Add(new NpgsqlParameter("expdate", expiryDate.SelectedDate));
                cmd.Parameters.Add(new NpgsqlParameter("timestamp", DateTime.Now));

                string mchName = Environment.MachineName.ToString();
                if (mchName == "DESKTOP-7L1HPPN")
                    mchName = "old_lakk_pc";

                cmd.Parameters.Add(new NpgsqlParameter("workstation", mchName));
                cmd.ExecuteNonQuery();
                //closing connection ASAP
                conn.Close();
                ((MainWindow)this.Owner).VarnishDisplay();

            }
            catch (Exception msg)
            {
                MessageBox.Show(msg.ToString());
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {

            if (RegexValidation(batchTbx.Text, "batchTxbx") && expiryDate.SelectedDate > DateTime.Today)
            {
                db_insert();
                uploadOutput.Text = "Adatbázishoz hozzáadva!";
                uploadOutput.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF09E409"));
                uploadOutput.FontWeight = FontWeights.UltraBold;
            }else
            {
                uploadOutput.Text = "Nem megfelelő adatok!";
                uploadOutput.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0505"));
                uploadOutput.FontWeight = FontWeights.Normal;
            }
        }

        // Syntax check of the product
        private void BatchValidator(object sender, TextChangedEventArgs e)
        {

            uploadOutput.Text = "";

            if (RegexValidation(batchTbx.Text, "batchTxbx"))
            {
                isBatchValid.Text = "OK";
                isBatchValid.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF09E409"));
                isBatchValid.FontWeight = FontWeights.UltraBold;
            }
            else
            {

                isBatchValid.Text = "Nem megfelelő formátum!";
                isBatchValid.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0505"));
                isBatchValid.FontWeight = FontWeights.Normal;
            }
        }

        private void DateValidator(object sender, SelectionChangedEventArgs e)
        {
            uploadOutput.Text = "";

            if (expiryDate.SelectedDate > DateTime.Today)
            {
                isDateValid.Text = "OK";
                isDateValid.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF09E409"));
                isDateValid.FontWeight = FontWeights.UltraBold;
            }else
            {
                isDateValid.Text = "Nem megfelelő dátum!";
                isDateValid.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0505"));
                isDateValid.FontWeight = FontWeights.Normal;
            }
        }

        private void ChangeDate(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int year = expiryDate.SelectedDate.Value.Year;
            int month = expiryDate.SelectedDate.Value.Month;

            if (actualDateValue < dateSlider.Value)
            {
                if (month == 12)
                {
                    year++;
                    month = 1;
                }
                else
                    month++;
            }
            else
            {
                if (month == 1)
                {
                    year--;
                    month = 12;
                }
                else
                    month--;
            }

            expiryDate.SelectedDate = new DateTime(year, month, 01);
            actualDateValue = dateSlider.Value;
        }

        private void batchTbxKeyUpEvent(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                dateSlider.Focus();
            }
        }

        private void dateSliderKeyUpEvent(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BackBtn.Focus();
                SaveBtn_Click(SaveBtn, new RoutedEventArgs());
            }
        }
    }


}


