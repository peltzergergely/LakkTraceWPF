using Npgsql;
using System;
using System.Configuration;
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

        //compares product with the regex rule
        private void ProductValidator()
        {
            if (RegexValidation(productTxbx.Text, "productTxbx"))
            {
                productLbl.Text = "Termék DM: " + productTxbx.Text;
                IsProductValidated = true;
            }
            else
            {
                productLbl.Text = "Termék DataMatrix nem megfelelő!";
                IsProductValidated = false;
            }
        }

        //compares carrier with regex rule
        private void CarrierValidator()
        {
            if (RegexValidation(carrierTxbx.Text, "carrierTxbx"))
            {
                carrierLbl.Text = "Keret DM: " + carrierTxbx.Text;
                IsCarrierValidated = true;
            }
            else
            {
                carrierLbl.Text = "Keret DataMatrix nem megfelelő!";
                IsCarrierValidated = false;
            }
        }

        //checks if both fields are valid
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

        //cleans the form for seamless data input
        private void FormCleaner()
        {
            productTxbx.Text = "";
            productLbl.Text = "";
            carrierTxbx.Text = "";
            carrierLbl.Text = "";
            dbresultLbl.Text = "";
            IsProductValidated = false;
            IsCarrierValidated = false;
            productTxbx.Focus();
            this.mainStackPanel.Background = new SolidColorBrush(Colors.White);
        }

        //this is where the error flashing and communication happens
        private void FormErrorDisplay()
        {
            productTxbx.Text = "";
            carrierTxbx.Text = "";
            IsProductValidated = false;
            IsCarrierValidated = false;
            productTxbx.Focus();
            this.mainStackPanel.Background = new SolidColorBrush(Colors.Red);
        }

        //separates the products for database operations 
        private string GetTableName()
        {
            if (carrierTxbx.Text.Contains("BMW"))
                return "bmw";
            else
                return "volvo";
        }

        //have to know which side is in only proceed if its TOP then BOT
        private string GetCarrierSide()
        {
            if (carrierTxbx.Text.Contains("TOP"))
                return "TOP";
            else
                return "BOT";
        }

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

            if (countProd == 0) // everything is fine the datamatrix is new in the database, proceed to upload
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
            else if (countProd == 1) // the TOP side is coated, proceed only if this is the bottom
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
            else // if its more than once in the database then the product's both side should be finished
            {
                dbresultLbl.Text = "EZ A TERMÉK MINDKÉT OLDALA LAKKOZOTT!";
                FormErrorDisplay();
            }
        }

        //insert values to database
        private void DbInsert(string table) //DB insert
        {
            try
            {
                string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
                // Making connection with Npgsql provider
                var conn = new NpgsqlConnection(connstring);
                conn.Open();
                // building SQL query
                var cmd = new NpgsqlCommand("INSERT INTO " + table + " (prod_dm, carr_dm, timestamp) VALUES(:prod_dm, :carr_dm, :timestamp)", conn);
                cmd.Parameters.Add(new NpgsqlParameter("prod_dm", productTxbx.Text));
                cmd.Parameters.Add(new NpgsqlParameter("carr_dm", carrierTxbx.Text));
                cmd.Parameters.Add(new NpgsqlParameter("timestamp", DateTime.Now));
                cmd.ExecuteNonQuery();
                //closing connection ASAP
                conn.Close();
            }
            catch (Exception msg)
            {
                MessageBox.Show(msg.ToString());
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
                FormCleaner();

            }
        }
    }
}
