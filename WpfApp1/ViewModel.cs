using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;

namespace WpfApp1
{
  public class ViewModel : INotifyPropertyChanged
  {
    public string InitialPath { get; set; } = @"c:\temp";
    public BitmapImage ImageToShow { get; set; }

    private string _status;
    public string Status { get => this._status; set { this._status = value; OnPropertyChanged(); } }

    private bool _isWorking = false;
    private bool IsWorking { get => this._isWorking; set { this._isWorking = value; OnPropertyChanged(nameof(Cmd)); } }
    public ICommand Cmd { get => new RelayCommand(CmdExec, CanCmdExec); }

    private int _id = 1;
    public int ID { get => this._id; set { this._id = value; OnPropertyChanged(); } }
    private void CmdExec(object obj)
    {
      switch (obj.ToString())
      {
        case "Create":
          CreataTable();
          break;
        case "Fill":
          InsertPictures(InitialPath);
          break;
        case "Show":
          ShowImage(ref this._id);
          break;
        default:
          break;
      }
    }
    private bool CanCmdExec(object obj) => (obj.ToString() == "Show") ? true : !this._isWorking;

    private async void CreataTable()
    {
      this.IsWorking = true;
      await Task.Factory.StartNew(() =>
      {
        try
        {
          using (SqlConnection cn = new SqlConnection(WpfApp1.Properties.Settings.Default.cnSQL))
          {
            cn.Open();
            using (SqlCommand cmd = new SqlCommand { Connection = cn })
            {
              // delete previous table in SQL Server 2016 and above
              cmd.CommandText = "DROP TABLE IF EXISTS Table1;";
              cmd.ExecuteNonQuery();
              // Create Table
              cmd.CommandText = "CREATE Table [Table1]([ID] int Identity, [FolderName] nvarchar(255), [FileName] nvarchar(255), [FileType] nvarchar(10), [Picture] image, CONSTRAINT [PK_Table1] PRIMARY KEY ([ID]))";
              cmd.ExecuteNonQuery();
            }
          }
          Status = "Table created";
        }
        catch (Exception ex) { Status = ex.Message; }
      });
      this.IsWorking = false;
    }

    private async void InsertPictures(string directorypath)
    {
      this.IsWorking = true;
      await Task.Factory.StartNew(() =>
      {
        int cnt = 0;
        try
        {
          SqlParameter parFolderName, parFileName, parFileType, parPicture;
          using (SqlConnection cn = new SqlConnection(WpfApp1.Properties.Settings.Default.cnSQL))
          {
            cn.Open();
            using (SqlCommand cmd = new SqlCommand { Connection = cn })
            {
              cmd.CommandText = "INSERT INTO dbo.Table1 (FolderName, FileName, FileType, Picture) " +
                                  "VALUES (@FolderName,@FileName,@FileType,@Picture); " +
                                  "SELECT CAST(scope_identity() AS int); ";
              // define parameters once
              parFolderName = cmd.Parameters.Add("@FolderName", SqlDbType.VarChar, 255);
              parFileName = cmd.Parameters.Add("@FileName", SqlDbType.VarChar, 255);
              parFileType = cmd.Parameters.Add("@FileType", SqlDbType.VarChar, 10);
              parPicture = cmd.Parameters.Add("@Picture", SqlDbType.Image);
              foreach (string f in GetFilesRecursive(directorypath))
              {
                Status = $"Loaded file {f}";
                using (Image img = Image.FromFile(f))
                {
                  int w = 800;
                  int h = (int)(w * img.Height / img.Width);
                  Bitmap bmp = new Bitmap(img, w, h);
                  using (MemoryStream ms = new MemoryStream())
                  {
                    bmp.Save(ms, ImageFormat.Jpeg);
                    parFolderName.Value = Path.GetDirectoryName(f);
                    parFileName.Value = Path.GetFileNameWithoutExtension(f);
                    parFileType.Value = Path.GetExtension(f);
                    parPicture.Value = ms.ToArray();
                    cmd.ExecuteNonQuery();
                  }
                }
                cnt++;
              }
            }
          }
        }
        catch (Exception ex) { Status = ex.Message; }
        Status = $"{cnt} pictures loaded";
      });
      this.IsWorking = false;
    }

    private IEnumerable<string> GetFilesRecursive(string directorypath)
    {
      foreach (string file in Directory.GetFiles(directorypath, "*.jpg")) yield return file;
      foreach (var folder in Directory.GetDirectories(directorypath)) GetFilesRecursive(folder);
    }

    private void ShowImage(ref int ID)
    {
      this.IsWorking = true;
      try
      {
        string msg = "no image loaded";
        using (SqlConnection cn = new SqlConnection(WpfApp1.Properties.Settings.Default.cnSQL))
        {
          cn.Open();
          using (SqlCommand cmd = new SqlCommand("SELECT * FROM Table1 WHERE ID = @ID", cn))
          {
            cmd.Parameters.AddWithValue("@ID", ID);
            SqlDataReader rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
              ImageToShow = LoadBitmapImage((byte[])rdr["Picture"]);
              OnPropertyChanged(nameof(ImageToShow));
              string fileName = rdr["FileName"].ToString() + "." + rdr["FileType"].ToString();
              msg = $"image loaded ID: {ID}, {fileName}";
              ID++;
            }
          }
        }
        Status = msg;
      }
      catch (Exception ex) { Status = ex.Message; }
      this.IsWorking = false;
    }

    private static BitmapImage LoadBitmapImage(byte[] imageData)
    {
      if (imageData == null || imageData.Length == 0) return null;
      var image = new BitmapImage();
      using (var mem = new MemoryStream(imageData))
      {
        mem.Position = 0;
        image.BeginInit();
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = null;
        image.StreamSource = mem;
        image.EndInit();
      }
      image.Freeze();
      return image;
    }

    #region PropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;
    internal void OnPropertyChanged([CallerMemberName] string propName = "") =>
     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    #endregion
  }

}
