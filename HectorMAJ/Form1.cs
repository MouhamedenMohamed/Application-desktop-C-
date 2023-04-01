using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Data.SQLite;
using System.Data.SqlClient;
using System.Configuration;
//using Microsoft.EntityFrameworkCore;

namespace Hector
{
    public partial class Form1 : Form
    {
        private string dbPath;
        private string csvFilePath;
        private SQLiteConnection dbConnection;
        public Form1()
        {
            InitializeComponent();

            //Chemin vers la base de donnees
            dbPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Hector.SQLite");
            // Configuration pour que le fichier SQLite soit copié dans le répertoire de sortie
            AppDomain.CurrentDomain.SetData("DataDirectory", Application.StartupPath);
            // Créer la base de données si elle n'existe pas
            CreateDatabaseIfNotExists();

            // Connecter à la base de données
            dbConnection = new SQLiteConnection($"Data Source={dbPath}");
            dbConnection.Open();

            // Mettre à jour l'interface graphique avec le contenu de la base de données
            RefreshTreeView();
        }

        private void splitContainer1_Panel1_Paint(object sender, PaintEventArgs e)
        {
            //splitContainer.Panel1MinSize = 200;
        }

        private void importerToolStripMenuItem_Click(object sender, EventArgs e)
        {

            
            // On cree la fenêtre modale
            Form importerForm = new Form();
            importerForm.Text = "Importer un fichier CSV";
            importerForm.Size = new Size(500, 200);
            importerForm.StartPosition = FormStartPosition.CenterParent;
            importerForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            importerForm.MaximizeBox = false;
            importerForm.MinimizeBox = false;

            // Ajouter les éléments à la fenêtre
            Label fileLabel = new Label();
            fileLabel.Text = "Sélectionner un fichier CSV :";
            fileLabel.Size = new Size(150, 15);
            fileLabel.Location = new Point(10, 10);
            importerForm.Controls.Add(fileLabel);

            TextBox fileTextBox = new TextBox();
            fileTextBox.Location = new Point(10, 30);
            fileTextBox.Size = new Size(140, 13);
            importerForm.Controls.Add(fileTextBox);

            Button browseButton = new Button();
            browseButton.Text = "Parcourir";
            browseButton.Location = new Point(220, 30);
            importerForm.Controls.Add(browseButton);

            //Bouton intégration en mode écrasement
            Button overwriteButton = new Button();
            overwriteButton.Text = "Mode écrasement";
            overwriteButton.Location = new Point(10, 70);
            overwriteButton.Size = new Size(100, 25);
            importerForm.Controls.Add(overwriteButton);

            //Bouton intégration en mode ajout
            Button addButton = new Button();
            addButton.Text = "Mode ajout";
            addButton.Location = new Point(150, 70);
            importerForm.Controls.Add(addButton);

            //Bar de progression
            ProgressBar progressBar = new ProgressBar();
            progressBar.Location = new Point(10, 110);
            progressBar.Size = new Size(360, 20);
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            importerForm.Controls.Add(progressBar);

            // Associer des événements aux boutons
            browseButton.Click += (s, ev) =>
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Fichiers CSV (*.csv)|*.csv";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    //fileTextBox.Text = openFileDialog.FileName;
                    csvFilePath = openFileDialog.FileName;
                    fileTextBox.Text = Path.GetFileName(csvFilePath);
                }
            };

            overwriteButton.Click += (s, ev) =>
            {
                if (csvFilePath != null)
                {
                    progressBar.Visible = true;
                    progressBar.Value = 0;
          
                    browseButton.Enabled = false;
                    overwriteButton.Enabled = false;
                    addButton.Enabled = false;

                    using (dbConnection)
                    {
                       // dbConnection.Open();
                        using (var transaction = dbConnection.BeginTransaction())
                        {
                            using (var reader = new StreamReader(csvFilePath))
                            {
                                var line = "";
                                var isFirstLine = true;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (isFirstLine)
                                    {
                                        isFirstLine = false;
                                        continue; // Skip first line (header)
                                    }

                                    var fields = line.Split(';');

                                    //Insertion du nom de famille dans la BD
                                    var sqlCommandFamille = new SQLiteCommand("INSERT INTO Familles(Nom) values(@nom)", dbConnection, transaction);
                                    sqlCommandFamille.Parameters.AddWithValue("@nom", fields[3]);
                                    sqlCommandFamille.ExecuteNonQuery();

                                    //On recupère la ref de la Famille pour l'inserer comme clé étrangère dans SousFamille
                                    string requete = "SELECT RefFamille FROM Familles WHERE Nom=@nom";
                                    SQLiteCommand commandeRefFamille = new SQLiteCommand(requete, dbConnection, transaction);
                                    commandeRefFamille.Parameters.AddWithValue("@Nom", fields[3]);
                                    int? refFamille=null;
                                    SQLiteDataReader readerRequete = commandeRefFamille.ExecuteReader();
                                    //object resultat;

                                    using (commandeRefFamille)
                                    {
                                        commandeRefFamille.Parameters.AddWithValue("@Nom", fields[3]);
                                        /**resultat = commande.ExecuteScalar();
                                        // Si le résultat n'est pas null et qu'il peut être converti en int, on stocke la valeur dans la variable refFamille
                                        if (resultat != null && int.TryParse(resultat.ToString(), out int parsedValue))
                                        {
                                            refFamille = parsedValue;
                                        }**/

                                        //On recupere le 1er element retourné
                                        if (readerRequete.Read())
                                        {
                                            refFamille = Convert.ToInt32(readerRequete.GetValue(0));
                                        
                                        }

                                    }
                                    //Insertion dans SousFamille
                                    var sqlCommandSousFamille = new SQLiteCommand("INSERT INTO SousFamilles(RefFamille,Nom) values(@nom, @refFamille)", dbConnection, transaction);
                                    sqlCommandSousFamille.Parameters.AddWithValue("@nom", fields[3]);
                                    sqlCommandSousFamille.Parameters.AddWithValue("@refFamille", refFamille);
                                    sqlCommandSousFamille.ExecuteNonQuery();
                                    //On recupère la ref de SousFamille
                                    requete = "SELECT RefSousFamille FROM SousFamilles WHERE Nom=@nom";
                                    SQLiteCommand commandeRefSousFamille = new SQLiteCommand(requete, dbConnection, transaction);
                                    commandeRefSousFamille.Parameters.AddWithValue("@Nom", fields[3]);
                                    int? refSousFamille = null;
                                    SQLiteDataReader readerRequeteRefSousFamille = commandeRefSousFamille.ExecuteReader();


                                    using (readerRequeteRefSousFamille)
                                    {
                                        

                                        if (readerRequeteRefSousFamille.Read())
                                        {
                                            refSousFamille = Convert.ToInt32(readerRequeteRefSousFamille.GetValue(0));

                                        }

                                    }


                                    //Insertion du nom de la marque dans la table Marque
                                    var sqlCommandMarque = new SQLiteCommand("INSERT INTO Marques(Nom) values(@nom)", dbConnection, transaction);
                                    sqlCommandMarque.Parameters.AddWithValue("@nom", fields[2]);
                                    sqlCommandMarque.ExecuteNonQuery();
                                    //On recupère la ref de la marque
                                    requete = "SELECT RefMarque FROM Marques WHERE Nom=@nom";
                                    SQLiteCommand commandeRefMarque = new SQLiteCommand(requete, dbConnection, transaction);
                                    commandeRefMarque.Parameters.AddWithValue("@Nom", fields[2]);
                                    int? refMarque = null;
                                    SQLiteDataReader readerRequeteRefMarque = commandeRefMarque.ExecuteReader();
                                    

                                    using (readerRequeteRefMarque)
                                    {


                                        if (!readerRequeteRefMarque.IsClosed && readerRequeteRefMarque.Read())
                                        {
                                            refMarque = Convert.ToInt32(readerRequeteRefMarque.GetValue(0));

                                        }

                                    }

                                    
                                    //Insertion des informations de l'article
                                    var sqlCommand = new SQLiteCommand("INSERT INTO Articles (Description, RefArticle, RefSousFamille, RefMarque, PrixHT, Quantite)" +
                                        " VALUES (@desc, @RefArticle, @RefSousFamille, @RefMarque, @PrixHT, @Quantite)", dbConnection, transaction);
                                    sqlCommand.Parameters.AddWithValue("@desc", fields[0]);
                                    sqlCommand.Parameters.AddWithValue("@RefArticle", fields[1]);
                                    sqlCommand.Parameters.AddWithValue("@RefSousFamille", refSousFamille);
                                    sqlCommand.Parameters.AddWithValue("@RefMarque", refMarque);
                                    sqlCommand.Parameters.AddWithValue("@PrixHT", fields[5]);
                                    sqlCommand.Parameters.AddWithValue("@Quantite", 0);
                                    sqlCommand.ExecuteNonQuery();

                                    progressBar.Value = (int)((double)reader.BaseStream.Position / (double)reader.BaseStream.Length * 100);
                                }
                            }
                            transaction.Commit();
                        }
                        dbConnection.Close();
                    }

                    progressBar.Visible = false;
                    browseButton.Enabled = true;
                    overwriteButton.Enabled = true;
                    addButton.Enabled = true;
                    MessageBox.Show("Importation terminée !");
                }
                else
                {
                    MessageBox.Show("Veuillez sélectionner un fichier CSV !");
                }
                // On lance l'intégration en mode écrasement//code à ecrire

                
            };
            addButton.Click += (s, ev) =>
            {
                // Code pour lancer l'intégration en mode ajout //Code à ecrire
                //string filePath = fileNameTextBox.Text;
                //ImporterEnModeAjout(filePath, progressBar);
                progressBar.Value = 80; 
            };

            // Désactiver le sous-menu pendant le traitement
            importerToolStripMenuItem.Enabled = false;

            // Exécuter le traitement en arrière-plan
            /**await Task.Run(() =>
             {
                // Code de traitement à exécuter en arrière-plan
                // ...

                // Exemple de traitement : Attendre 5 secondes
                Task.Delay(5000).Wait();
             });**/

            // Réactiver le sous-menu une fois le traitement terminé
            importerToolStripMenuItem.Enabled = true;

            // Afficher un message pour indiquer que le traitement est terminé
            //MessageBox.Show("Importation terminée !");



            // Afficher la fenêtre modale
            importerForm.ShowDialog();
        }


        // Fonction d'importation en mode ajout
        private void ImporterEnModeAjout(string filePath, ProgressBar progressBar)
        {
            
        }

        // Fonction pour ajouter les nouveaux éléments
        private void AjouterNouveauxElements(DataTable divergentsTable)
        {
            // Ajouter les nouveaux éléments à la base de données
        }

        // Fonction pour mettre à jour les éléments divergents
        private void MettreAJourElements(DataTable divergentsTable)
        {
            // Mettre à jour les éléments divergents dans la base de données
        }

        /**private DataTable ChargerDonneesCSV(string filePath)
        {

        }**/




        private void CreateDatabaseIfNotExists()
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            using (var connection = new SQLiteConnection($"Data Source={dbPath}"))
            {
                connection.Open();

                // Créer la table "products" si elle n'existe pas
                using (var command = new SQLiteCommand("CREATE TABLE IF NOT EXISTS products (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, quantity INTEGER)", connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void RefreshTreeView()
        {
            // Vider le TreeView actuel
            treeView1.Nodes.Clear();

            // Sélectionner tous les produits dans la base de données
            var products = new DataTable();
            using (var command = new SQLiteCommand("SELECT * FROM products", dbConnection))
            {
                using (var adapter = new SQLiteDataAdapter(command))
                {
                    adapter.Fill(products);
                }
            }

            // Ajouter chaque produit au TreeView
            foreach (DataRow product in products.Rows)
            {
                var id = (long)product["id"];
                var name = (string)product["name"];
                var quantity = (int)product["quantity"];

                var node = new TreeNode($"{name} ({quantity})");
                node.Tag = id;

                treeView1.Nodes.Add(node);
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {

        }
        private void IntegrerDonnées(DataTable dataTable, ProgressBar progressBar)
        {

        }

        
    }




    
}
