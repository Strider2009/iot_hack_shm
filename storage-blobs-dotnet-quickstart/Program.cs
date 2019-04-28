//------------------------------------------------------------------------------
//MIT License

//Copyright(c) 2017 Microsoft Corporation. All rights reserved.

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
//------------------------------------------------------------------------------


namespace storage_blobs_dotnet_quickstart
{
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using System.Linq;
    using System.Threading;

    using System.Drawing;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;
    using System.Drawing.Drawing2D;

    public static class Program
    {
       
        public static int Main(string[] args)
        {
            Console.WriteLine("Azure Blob storage - .NET Quickstart sample");
            Console.WriteLine();

            var dirPath = args[0];
            if (!Directory.Exists(dirPath))
            {
                Console.WriteLine("Directory does not exists: ", dirPath);
                return 1;
            }

            // upload all the files 
            var files = Directory.GetFiles(args[0]);

            // loop waiting for the files to be processed
            Thread t1 = new Thread(() => Upload(files));
            Thread t2 = new Thread(() => Download());

            t1.Start();
            t2.Start();

            t1.Join();
            t2.Join();


            Console.WriteLine("Press any key to exit the sample application.");
            Console.ReadLine();
            return 0;
        }

        private static void Upload(String[] files)
        {
            while (true)
            {
                foreach (String file in files)
                {
                    ProcessAsync(file).GetAwaiter().GetResult();
                    Thread.Sleep(6000);
                }
            }
        }

        private static void Download()
        {
            while (true)
            {
                ProcessBlobsAsync().GetAwaiter().GetResult();
                Thread.Sleep(6000);
            }
        }

        private static async Task ProcessAsync(string sourceFile)
        {
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;
            string storageConnectionString = Environment.GetEnvironmentVariable("azureconnectionstring");
            //string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=iothackshm;AccountKey=FQ2yQ59uInxiGRsBUTURPPA4lb/wbx38KRzfTOWLtAXADejNA9KYRQ+uTEyIAT0j1+ZippOM1o6xWzBpkwDt0Q==;EndpointSuffix=core.windows.net";

            // Check whether the connection string can be parsed.
            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                    // Create a container called 'quickstartblobs' and append a GUID value to it to make the name unique. 
                    cloudBlobContainer = cloudBlobClient.GetContainerReference("iot-hack-shm");
                    await cloudBlobContainer.CreateIfNotExistsAsync();

                    // Set the permissions so the blobs are public. 
                    BlobContainerPermissions permissions = new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    };
                    await cloudBlobContainer.SetPermissionsAsync(permissions);

                    Console.WriteLine("Temp file = {0}", sourceFile);
                    Console.WriteLine("Uploading to Blob storage as blob '{0}'", sourceFile);
                    Console.WriteLine();

                    string fileName = Path.GetFileNameWithoutExtension(sourceFile);

                    // Get a reference to the blob address, then upload the file to the blob.
                    // Use the value of localFileName for the blob name.
                    CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(fileName);
                    await cloudBlockBlob.UploadFromFileAsync(sourceFile);

                    Console.WriteLine("Done uploading!");
                    // do app specific stuff 
                }
                catch (StorageException ex)
                {
                    Console.WriteLine("Error returned from the service: {0}", ex.Message);
                }
            }
            else
            {
                Console.WriteLine(
                    "A connection string has not been defined in the system environment variables. " +
                    "Add a environment variable named 'storageconnectionstring' with your storage " +
                    "connection string as a value.");
            }
        }

        private static async Task ProcessBlobsAsync()
        {
            String containerReferenceName = "iot-hack-shm-results";
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;
            string storageConnectionString = Environment.GetEnvironmentVariable("azureconnectionstring");

            // Check whether the connection string can be parsed.
            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                    cloudBlobContainer = cloudBlobClient.GetContainerReference(containerReferenceName);

                    Boolean blobContainerExists = await cloudBlobContainer.ExistsAsync();
                    if (!blobContainerExists)
                    {
                        Console.WriteLine("Container doesn't exist yet, please re-run when it does.");
                    }

                    // Set the permissions so the blobs are public. 
                    BlobContainerPermissions permissions = new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    };
                    await cloudBlobContainer.SetPermissionsAsync(permissions);



                    // Get a reference to the blob address, then upload the file to the blob.
                    // Use the value of localFileName for the blob name.
                    BlobContinuationToken token = null;
                    var blobResultSegment = await cloudBlobContainer.ListBlobsSegmentedAsync(token);
                    var blobs = blobResultSegment.Results;
                    string tempDir = Path.Combine(Path.GetTempPath(), "iothackshm");
                    Directory.CreateDirectory(tempDir);
                    foreach (CloudBlockBlob blob in blobs)
                    {
                        string filePath = Path.Combine(tempDir, blob.Name);
                        await blob.DownloadToFileAsync(filePath, FileMode.Create);
                        await blob.DeleteAsync();


                        String newImage = filePath.Replace(".json", ".jpg");
                        String originalImage = String.Format("C:\\Users\\peanderson\\Downloads\\test\\{0}", blob.Name.Replace(".json", ".jpg"));
                        string json = File.ReadAllText(filePath);
                        JObject jsonData = JObject.Parse(json);
                        foreach (JObject item in jsonData["data"][0]["data"]["objects"])
                        {
                            // Create image.
                            // File.Copy(originalImage, newImage, true);
                            using (Image image = Image.FromFile(originalImage))
                            {
                                using (var graphics = Graphics.FromImage(image))
                                {
                                    using (var pen = new Pen(Color.HotPink, 2))
                                    {
                                        foreach (var dataObj in item)
                                        {
                                            if (dataObj.Key != "rectangle") continue;
                                            int x = int.Parse(dataObj.Value["x"].ToString());
                                            int y = int.Parse(dataObj.Value["y"].ToString());
                                            int w = int.Parse(dataObj.Value["w"].ToString());
                                            int h = int.Parse(dataObj.Value["h"].ToString());

                                            graphics.DrawRectangle(pen, new Rectangle(x, y, w, h));
                                        }
                                    }

                                    graphics.Save();
                                }
                                image.Save(newImage);
                            }
                        }
                    }
                
                    Console.WriteLine("Done downloading!");
                    // do app specific stuff 
                }
                catch (StorageException ex)
                {
                    Console.WriteLine("Error returned from the service: {0}", ex.Message);
                }
            }
            else
            {
                Console.WriteLine(
                    "A connection string has not been defined in the system environment variables. " +
                    "Add a environment variable named 'storageconnectionstring' with your storage " +
                    "connection string as a value.");
            }
        }

        private static void AnnotateImage(String imagePath)
        {
            

        }
    }
}
