using Npgsql;
using System;
using System.Configuration;
using System.Data;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LakkTraceWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool IsProductValidated { get; set; }

        public bool IsCarrierValidated { get; set; }

        public MainWindow()
        {
            //make first textbox focused
            Loaded += (sender, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            InitializeComponent();
            DailyProduction();
            VarnishDisplay();
        }

        //on Enter jumps to next focus, also triggers datavalidation
        private void TxtBxKeyUpEvent(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement keyboardFocus = Keyboard.FocusedElement as UIElement;

                if (keyboardFocus != null)
                {
                    keyboardFocus.MoveFocus(tRequest);
                }
                e.Handled = true;

                if (IsBothValid())
                {
                    //time to call the database checking, because both field validated
                    SaveBtn_Click(sender, e);
                }

                if (Keyboard.FocusedElement == SaveBtn && !IsBothValid())
                {
                    FormErrorDisplay();
                }
            }

            if (Keyboard.FocusedElement == productTxbx && productTxbx.Text.Length > 0) //calls the validator for the field in focus
            {
                ProductValidator();
            }

            if (Keyboard.FocusedElement == carrierTxbx && carrierTxbx.Text.Length > 0)
            {
                CarrierValidator();
            }
        }

        //calling the validation regex from the app.config
        public bool RegexValidation(string dataToValidate, string datafieldName)
        {
            string rgx = ConfigurationManager.AppSettings[datafieldName];
            return (Regex.IsMatch(dataToValidate, rgx));
        }

        // Syntax check of the product
        private void ProductValidator()
        {
            if (RegexValidation(productTxbx.Text, "productTxbx"))
            {
                productLbl.Text = productTxbx.Text;
                IsProductValidated = true;
            }
            else
            {
                productLbl.Text = "Termék DataMatrix nem megfelelő!";
                IsProductValidated = false;
            }
        }

        // Syntax check of the carrier 
        private void CarrierValidator()
        {
            if (RegexValidation(carrierTxbx.Text, "carrierTxbx"))
            {
                carrierLbl.Text = carrierTxbx.Text;
                IsCarrierValidated = true;
            }
            else
            {
                carrierLbl.Text = "Keret DataMatrix nem megfelelő!";
                IsCarrierValidated = false;
            }
        }

        // Check if both Syntax checked
        private bool IsBothValid()
        {
            if (IsProductValidated == true && IsCarrierValidated == true)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // Cleans the form for seamless data input
        private void FormCleanerOnUploadFinished()
        {
            productTxbx.Text = "";
            carrierTxbx.Text = "";
            IsProductValidated = false;
            IsCarrierValidated = false;
            productTxbx.Focus();
            this.mainStackPanel.Background = new SolidColorBrush(Colors.White);
        }

        // Turns form red and keeps error message displayed on attempt to send wrong data
        private void FormErrorDisplay()
        {
            productTxbx.Text = "";
            carrierTxbx.Text = "";
            IsProductValidated = false;
            IsCarrierValidated = false;
            productTxbx.Focus();
            this.dbresultLbl.Foreground = Brushes.Green;
            this.mainStackPanel.Background = new SolidColorBrush(Colors.Red);
        }

        // Gets product from carrier DM 
        private string GetTableName()
        {
            if (carrierTxbx.Text.Contains("BMW"))
                return "bmw";
            else
                return "volvo";
        }

        // Gets TOP/BOT from carrier DM
        private string GetCarrierSide()
        {
            if (carrierTxbx.Text.Contains("TOP"))
                return "TOP";
            else
                return "BOT";
        }

        // Validates before data upload
        private void DbValidation(string table)
        {
            string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
            // Making connection
            var conn = new NpgsqlConnection(connstring);
            conn.Open();
            // building query
            var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM " + table + " WHERE prod_dm = :prod_dm", conn);
            cmd.Parameters.Add(new NpgsqlParameter("prod_dm", productTxbx.Text));
            Int32 countProd = Convert.ToInt32(cmd.ExecuteScalar());
            conn.Close();

            if (countProd == 0) // If the product DM is not in the DB and is TOP side up then uploads
            {
                if (GetCarrierSide() == "TOP")
                {
                    DbInsert(table);
                }
                else
                {
                    dbresultLbl.Text = "TOP OLDALT LAKKOZD ELŐSZÖR!";
                    FormErrorDisplay();
                }
            }
            else if (countProd == 1) // Only allows BOT if it's already in the DB (see above)
            {
                if (GetCarrierSide() == "BOT")
                {
                    DbInsert(table);
                }
                else
                {
                    dbresultLbl.Text = "ENNEK A TERMÉKNEK EZ AZ OLDALA MÁR LAKKOZVA VAN!";
                    FormErrorDisplay();
                }
                
            }
            else // By this time product should be finished
            {
                dbresultLbl.Text = "EZ A TERMÉK MINDKÉT OLDALA LAKKOZOTT!";
                FormErrorDisplay();
            }
        }

        // Insert values to database
        private void DbInsert(string table) //DB insert
        {
            try
            {
                string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
                // Making connection with Npgsql provider
                var conn = new NpgsqlConnection(connstring);
                var UploadMoment = DateTime.Now;
                conn.Open();
                // building SQL query
                var cmd = new NpgsqlCommand("INSERT INTO " + table + " (prod_dm, carr_dm, timestamp) VALUES(:prod_dm, :carr_dm, :timestamp)", conn);
                cmd.Parameters.Add(new NpgsqlParameter("prod_dm", productTxbx.Text));
                cmd.Parameters.Add(new NpgsqlParameter("carr_dm", carrierTxbx.Text));
                cmd.Parameters.Add(new NpgsqlParameter("timestamp", UploadMoment));
                cmd.ExecuteNonQuery();
                //closing connection ASAP
                conn.Close();
                dbresultLbl.Text = UploadMoment.ToString("HH:mm:ss") + "-kor adatok mentve.";
                FormCleanerOnUploadFinished();
            }
            catch (Exception msg)
            {
                MessageBox.Show(msg.ToString());
            }
        }

        // Shows the number of products uploaded this day
        private void DailyProduction()
        {
            string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
            // Making connection
            var conn = new NpgsqlConnection(connstring);
            conn.Open();
            // building query
            var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM bmw WHERE timestamp > 'today'", conn);
            Int32 countbmw = Convert.ToInt32(cmd.ExecuteScalar());
            cmd = new NpgsqlCommand("SELECT COUNT(*) FROM volvo WHERE timestamp > 'today'", conn);
            Int32 countvolvo = Convert.ToInt32(cmd.ExecuteScalar());
            conn.Close();
            dailyProductionLbl.Text = "Mai napon lakkozva \r\n";
            dailyProductionLbl.Text += "BMW db:";
            dailyProductionLbl.Text += " " + countbmw.ToString();
            dailyProductionLbl.Text += "\r\nVolvo db:";
            dailyProductionLbl.Text += " " + countvolvo.ToString();
            int sum = countvolvo + countbmw;
            if (sum % 100 == 0)
            {
                MessageBox.Show("Ellenőrizd a lakk mennyiségét! Ha utántöltés szükséges akkor kattints a 'Lakk feltöltése gombra!' ");
            }
        }

        // Shows the current varnish in a gridView
        private DataSet ds = new DataSet();
        private DataTable dt = new DataTable();

        private void VarnishDisplay()
        {
            try
            {
                // connstring stored in App.config
                string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
                var conn = new NpgsqlConnection(connstring);
                conn.Open();
                string sql = "SELECT batch, expdate, timestamp" +
                    " FROM lakk order by timestamp desc limit 2";
                // data adapter making request from our connection
                var da = new NpgsqlDataAdapter(sql, conn);
                ds.Reset();
                // filling DataSet with result from NpgsqlDataAdapter
                da.Fill(ds);
                // since it C# DataSet can handle multiple tables, we will select first
                dt = ds.Tables[0];
                // connect grid to DataTable
                dataGridView1.ItemsSource = dt.AsDataView();
                // since we only showing the result we don't need connection anymore
                conn.Close();
            }
            catch (Exception msg)
            {
                // error handling
                MessageBox.Show(msg.ToString());
                //throw;
            }
        }

        //closes the app
        private void MainMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        //savebutton for collecting the final elements.
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (IsBothValid())
            {
                DbValidation(GetTableName());
            }
            DailyProduction();
            VarnishDisplay();
        }
    }
}
