using Amazon;
using Octokit;
using Amazon.S3;
using Amazon.S3.Model;

using System;
using System.IO;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Configuration;
using System.Web.UI.DataVisualization.Charting;

namespace RepoStatsV2
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            #region Read app settings

            var githubToken = ConfigurationManager.AppSettings["GitHubToken"];
            var awsAccessKey = ConfigurationManager.AppSettings["AWSAccessKey"];
            var awsSecretKey = ConfigurationManager.AppSettings["AWSSecretKey"];

            #endregion

            #region Fetch Public Repos

            // -----------------------------------------------------
            // Initialize the Octokit GitHubClient with the provided
            // token and grab the username of the authenticated user.

            var client = new GitHubClient(new ProductHeaderValue("MatthewBentz"))
            {
                Credentials = new Credentials(githubToken)
            };
            var username = client.User.Current().Result.Login;
            Console.WriteLine($"Authenticated User: {username}\n");

            // --------------------------------------------------
            // Fill the list of public repositories for the user.

            var repos = client.Repository.GetAllForUser(username).Result
                .Where(repo => !repo.Private);
            
            Console.WriteLine($"{repos.Count()} public repositories found.\n");

            // --------------------------------------
            // Local directory to save the PNG files.

            if (!Directory.Exists("Charts"))
            {
                Directory.CreateDirectory("Charts");
            }

            #endregion

            foreach (var repo in repos)
            {
                #region Create the chart and export it to a PNG file

                // --------------------------------------------------
                // Grab the recent views from each public repository.

                var views = client.Repository.Traffic.GetViews(repo.Id, new RepositoryTrafficRequest(TrafficDayOrWeek.Day)).Result;

                // ----------------------------------
                // Create a chart for the repository.
                // ----------------------------------

                var chart = new Chart();
                var chartArea = new ChartArea("Default");
                chart.ChartAreas.Add(chartArea);

                // ----------------------
                // Add views to the chart

                chart.Series.Add(new Series("datedViews"));

                for (int i = 0; i < 14; i++)
                {
                    // Loop through the days that don't return a view object, and default to 0.

                    var date = DateTime.Today.AddDays(-13 + i);
                    var viewCount = views.Views.SingleOrDefault(v => v.Timestamp.Date == date)?.Count ?? 0;

                    chart.Series["datedViews"].Points.AddXY(date.ToString("M/dd"), viewCount);
                }

                // ------------------------
                // Chart styling light mode

                var title = new Title($"Recent Views for {repo.Name}")
                {
                    Font = new Font("Arial", 26, FontStyle.Bold)
                };
                chart.Titles.Add(title);
                chart.Width = 1000;
                chart.Height = 700;
                chart.Palette = ChartColorPalette.SeaGreen;

                chartArea.AxisX.Interval = 1;
                chartArea.AxisX.LabelStyle.Angle = 45;
                chartArea.AxisX.MajorGrid.Enabled = false;
                chartArea.AxisX.LabelStyle.Font = new Font("Arial", 16, FontStyle.Regular);

                chartArea.AxisY.MajorGrid.Enabled = false;
                chartArea.AxisY.LabelStyle.Font = new Font("Arial", 16, FontStyle.Regular);

                chartArea.BackImageWrapMode = ChartImageWrapMode.Unscaled;
                chartArea.BackImageAlignment = ChartImageAlignmentStyle.Center;
                chartArea.BackImage = Path.Combine(Directory.GetCurrentDirectory(), "Media\\auburn-logo-color.png");

                // ---------------------------------
                // Export the chart image to a file.

                chart.SaveImage($"Charts\\{repo.Name}_ViewsChart.png", ChartImageFormat.Png);

                // -----------------------
                // Chart styling dark mode

                title.ForeColor = Color.White;

                chartArea.AxisX.LineColor = Color.White;
                chartArea.AxisX.LabelStyle.ForeColor = Color.White;
                chartArea.AxisX.MajorTickMark.LineColor = Color.White;

                chartArea.AxisY.LineColor = Color.White;
                chartArea.AxisY.LabelStyle.ForeColor = Color.White;
                chartArea.AxisY.MajorTickMark.LineColor = Color.White;

                chart.BackColor = Color.FromArgb(13, 17, 23);
                chartArea.BackColor = Color.FromArgb(13, 17, 23);

                chartArea.BackImage = Path.Combine(Directory.GetCurrentDirectory(), "Media\\auburn-logo-white.png");

                // ---------------------------------
                // Export the chart image to a file.

                chart.SaveImage($"Charts\\{repo.Name}_ViewsChart_Dark.png", ChartImageFormat.Png);

                #endregion

                #region Post the image to an S3 bucket

                // ------------------------------------------
                // Post the saved chart PNG to the S3 bucket.
                // ------------------------------------------

                // -----
                // Light

                var s3Client = new AmazonS3Client(awsAccessKey, awsSecretKey, RegionEndpoint.USEast2);

                var requestLight = new PutObjectRequest
                {
                    BucketName = "repostatscharts",
                    Key = $"MatthewsRepos/{repo.Name}_ViewsChart.png",
                    InputStream = new MemoryStream(File.ReadAllBytes($"Charts\\{repo.Name}_ViewsChart.png")),
                };
                requestLight.Headers.CacheControl = "no-cache"; 

                s3Client.PutObject(requestLight);
                var objectUrl = $"https://repostatscharts.s3.us-east-2.amazonaws.com/MatthewsRepos/{repo.Name}_ViewsChart.png";

                Console.WriteLine($"{repo.Name} - {objectUrl} (Light)");

                // ----
                // Dark

                var requestDark = new PutObjectRequest
                {
                    BucketName = "repostatscharts",
                    Key = $"MatthewsRepos/{repo.Name}_ViewsChart_Dark.png",
                    InputStream = new MemoryStream(File.ReadAllBytes($"Charts\\{repo.Name}_ViewsChart_Dark.png")),
                };
                requestDark.Headers.CacheControl = "no-cache";

                s3Client.PutObject(requestDark);
                objectUrl = $"https://repostatscharts.s3.us-east-2.amazonaws.com/MatthewsRepos/{repo.Name}_ViewsChart_Dark.png";

                Console.WriteLine($"{repo.Name} - {objectUrl} (Dark)");

                #endregion
            }
        }
    }
}