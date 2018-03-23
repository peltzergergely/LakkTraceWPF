using Npgsql;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
    /// Interaction logic for DatabaseWindow.xaml
    /// </summary>
    public partial class DatabaseWindow : Window
    {
        public DatabaseWindow()
        {
            InitializeComponent();
            SettingUpTheParameters();
        }

        public IDictionary<string, string> prodItems = new Dictionary<string, string>();
        public IDictionary<string, string> workStationItems = new Dictionary<string, string>();

        private void SettingUpTheParameters()
        {
            // set back the short date pattern to dd-MM-yyyy after lacquer load (MM-yyyy)
            CultureInfo ci = CultureInfo.CreateSpecificCulture(CultureInfo.CurrentCulture.Name);
            ci.DateTimeFormat.ShortDatePattern = "dd-MM-yyyy";
            Thread.CurrentThread.CurrentCulture = ci;

            // Starting and Ending date
            startDate.SelectedDate = new DateTime(2017, 10, 01);
            endDate.SelectedDate = DateTime.Today;

            // Add items to product's combobox
            prodItems.Add("BMW", "bmw");
            prodItems.Add("Volvo", "volvo");
            prodItems.Add("Lakk", "lakk");

            prodCbx.ItemsSource = prodItems;
            prodCbx.DisplayMemberPath = "Key";
            prodCbx.SelectedValuePath = "Value";
            prodCbx.SelectedIndex = 0;

            // Add items to workstation's combobox
            workStationItems.Add("Régi állomás", "'old_lakk_pc'");
            workStationItems.Add("Új állomás", "'DESKTOP-BVFFOIU'");
            workStationItems.Add("Összes", "'old_lakk_pc' or workstation LIKE '%' or workstation is null");

            workStationCbx.ItemsSource = workStationItems;
            workStationCbx.DisplayMemberPath = "Key";
            workStationCbx.SelectedValuePath = "Value";
            workStationCbx.SelectedIndex = 0;

        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private DataSet ds = new DataSet();
        private DataTable dt = new DataTable();

        private void ListBtn_Click(object sender, RoutedEventArgs e)
        {
            using (new WaitCursor())
            {
                try
                {
                    // connstring stored in App.config
                    string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
                    var conn = new NpgsqlConnection(connstring);
                    conn.Open();
                    string sql = getSQLcommand();
                    // data adapter making request from our connection
                    var da = new NpgsqlDataAdapter(sql, conn);
                    ds.Reset();
                    // filling DataSet with result from NpgsqlDataAdapter
                    da.Fill(ds);
                    // since it C# DataSet can handle multiple tables, we will select first
                    dt = ds.Tables[0];
                    // connect grid to DataTable
                    resultDataGrid.ItemsSource = dt.DefaultView;
                    // since we only showing the result we don't need connection anymore
                    conn.Close();

                    setHeaderTexts();
                    resultRowCount.Content = resultDataGrid.Items.Count.ToString();
                }
                catch (Exception msg)
                {
                    MessageBox.Show("Hiba történt! Részletek elmentve az Errors mappába!");
                    ErrorLog.CreateErrorLog(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString());
                }
            }
            
        }

        private void setHeaderTexts()
        {
            
            if (prodCbx.Text.ToString() == "Lakk")
            {
                resultDataGrid.Columns[0].Header = "ID";
                resultDataGrid.Columns[1].Header = "Batch";
                resultDataGrid.Columns[2].Header = "Lejárat";
                resultDataGrid.Columns[3].Header = "Beviteli dátum";
                resultDataGrid.Columns[4].Header = "Munkaállomás";
            }
            else
            {
                resultDataGrid.Columns[0].Header = "ID";
                resultDataGrid.Columns[1].Header = "Cikkszám";
                resultDataGrid.Columns[2].Header = "Keret";
                resultDataGrid.Columns[3].Header = "Beviteli dátum";
                resultDataGrid.Columns[4].Header = "Munkaállomás";
            }
        }

        private string getSQLcommand()
        {

            string start = "'" + startDate.SelectedDate.Value.Year.ToString() + "-" + startDate.SelectedDate.Value.Month.ToString() + "-" + startDate.SelectedDate.Value.Day.ToString() + "'";
            string end = "'" + endDate.SelectedDate.Value.Year.ToString() + "-" + endDate.SelectedDate.Value.Month.ToString() + "-" + endDate.SelectedDate.Value.Day.ToString() + "'";

            string Querycmd = "SELECT * FROM " + prodCbx.SelectedValue.ToString() + " WHERE (workstation = " + workStationCbx.SelectedValue.ToString() + ") AND date(timestamp) >= " + start + " and date(timestamp) <= " + end;

            if (prodDmTbx.Text.Length > 0)
            {
                Querycmd = Querycmd + " AND prod_dm = '" + prodDmTbx.Text + "'";
            }


            return Querycmd;
        }

        private void prodCbx_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (prodCbx.SelectedValue.ToString() == "lakk")
            {
                prodDmTbx.IsEnabled = false;
                prodDmTbx.Text = "";
                prodDmTbx.Background = Brushes.LightCyan;
            }
            else
            {
                prodDmTbx.IsEnabled = true;
                prodDmTbx.Background = Brushes.White;
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            startDate.SelectedDate = new DateTime(2017, 10, 01);
            endDate.SelectedDate = DateTime.Today;
            prodCbx.SelectedIndex = 0;
            workStationCbx.SelectedIndex = 0;
            prodDmTbx.Text = "";
            resultDataGrid.ItemsSource = null;
            resultRowCount.Content = "0";
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ListBtn_Click(this,e);
            }
        }
    }
}
