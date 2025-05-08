using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using openCV;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;


namespace AIOI
{
    public partial class Form1 : Form
    {
        PictureBox pictureBox1;
        IplImage originalImage; // To store the original image for manipulation
        IplImage CoriginalImage; // To store the converted original image for manipulation
        IplImage image1;
        IplImage img;
        private string currentImagePath;
        private float zoomFactor = 1.0f;

        public Form1()
        {
            InitializeComponent();

            // Set form to open in the center of the screen
            this.StartPosition = FormStartPosition.CenterScreen;

            // Initialize image variables to prevent null reference exceptions
            originalImage = new IplImage();
            CoriginalImage = new IplImage();
            img = new IplImage();
            image1 = new IplImage();
            zoomFactor = 1.0f;

            // Initialize PictureBox
            pictureBox1 = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom, // Maintain aspect ratio
                BorderStyle = BorderStyle.None // Set border width to zero
            };
            this.Controls.Add(pictureBox1);

            // Center PictureBox and update image when form loads or resizes
            this.Resize += (s, e) =>
            {
                CenterPictureBox();
                if (CoriginalImage.width > 0) // Changed from CoriginalImage != null
                {
                    UpdateImageOnResize(CoriginalImage); // Use CoriginalImage for resizing
                }
            };
            CenterPictureBox();
        }

        private void CenterPictureBox()
        {
            // Don't resize if the form is minimized
            if (this.WindowState == FormWindowState.Minimized)
                return;

            // Set PictureBox size (e.g., 80% of form's client area)
            int width = (int)(this.ClientSize.Width * 0.8);
            int height = (int)(this.ClientSize.Height * 0.8);
            pictureBox1.Size = new Size(width, height);

            // Center PictureBox in the form
            pictureBox1.Location = new Point(
                (this.ClientSize.Width - pictureBox1.Width) / 2,
                (this.ClientSize.Height - pictureBox1.Height) / 2
            );
        }


        private void UpdateImageOnResize(IplImage imageToPreview)
        {
            // Only update if an image has valid dimensions
            // Don't update if the form is minimized or image is invalid
            if (this.WindowState == FormWindowState.Minimized || imageToPreview.width <= 0 || imageToPreview.height <= 0)
                return;

            try
            {
                // Create a copy for preview (image1)
                image1 = cvlib.CvCreateImage(new CvSize(imageToPreview.width, imageToPreview.height), imageToPreview.depth, imageToPreview.nChannels);
                cvlib.CvCopy(ref imageToPreview, ref image1);

                // Get image dimensions (for preview resizing)
                int origWidth = image1.width;
                int origHeight = image1.height;

                // Calculate target dimensions while preserving aspect ratio
                float aspectRatio = (float)origWidth / origHeight;
                int targetWidth, targetHeight;

                if (aspectRatio > (float)pictureBox1.Width / pictureBox1.Height)
                {
                    // Image is wider than PictureBox
                    targetWidth = pictureBox1.Width;
                    targetHeight = (int)(targetWidth / aspectRatio);
                }
                else
                {
                    // Image is taller than PictureBox
                    targetHeight = pictureBox1.Height;
                    targetWidth = (int)(targetHeight * aspectRatio);
                }

                // Resize image1 for preview using OpenCV
                CvSize size = new CvSize(targetWidth, targetHeight);
                IplImage resizedImage = cvlib.CvCreateImage(size, image1.depth, image1.nChannels);
                cvlib.CvResize(ref image1, ref resizedImage, cvlib.CV_INTER_LINEAR);

                // Dispose of the previous image to free memory
                if (pictureBox1.Image != null)
                {
                    pictureBox1.Image.Dispose();
                }

                // Convert resized IplImage to System.Drawing.Image for preview
                pictureBox1.Image = (System.Drawing.Image)resizedImage;

                // Release temporary images to free memory
                cvlib.CvReleaseImage(ref image1);
                cvlib.CvReleaseImage(ref resizedImage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resizing image: {ex.Message}");
            }
        }

        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "JPG|*.jpg|JPEG|*.jpeg|Bitmap|*.bmp|PNG|*.png|All|*.*";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                currentImagePath = openFileDialog1.FileName; // Add this line after loading 
                    // Release any previously loaded images to free memory
                    if (originalImage.width > 0 && originalImage.height > 0)
                    {
                        cvlib.CvReleaseImage(ref originalImage);
                    }
                    if (CoriginalImage.width > 0 && CoriginalImage.height > 0)
                    {
                        cvlib.CvReleaseImage(ref CoriginalImage);
                    }

                    // Load the original image with error handling
                    string filePath = openFileDialog1.FileName;
                    if (!System.IO.File.Exists(filePath))
                    {
                        throw new Exception($"File not found: {filePath}");
                    }

                    // Try alternative loading approach for problematic images
                    try
                    {
                        // First try loading with OpenCV
                        originalImage = cvlib.CvLoadImage(filePath, cvlib.CV_LOAD_IMAGE_COLOR);
                        
                        // Check if the image was loaded successfully
                        if (originalImage.width <= 0 || originalImage.height <= 0)
                        {
                            // If OpenCV fails, try loading with System.Drawing first
                            using (Bitmap bmp = new Bitmap(filePath))
                            {
                                // Create a new IplImage with the dimensions of the bitmap
                                originalImage = cvlib.CvCreateImage(new CvSize(bmp.Width, bmp.Height), unchecked((int)cvlib.IPL_DEPTH_8U), 3);
                                
                                // Lock the bitmap data
                                System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits(
                                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                                    System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                                
                                // Copy the bitmap data to the IplImage
                                unsafe
                                {
                                    byte* srcPtr = (byte*)bmpData.Scan0.ToPointer();
                                    byte* dstPtr = (byte*)originalImage.imageData.ToPointer();
                                    
                                    for (int y = 0; y < bmp.Height; y++)
                                    {
                                        for (int x = 0; x < bmp.Width; x++)
                                        {
                                            int srcIndex = y * bmpData.Stride + x * 3;
                                            int dstIndex = y * originalImage.widthStep + x * 3;
                                            
                                            // BGR order for OpenCV
                                            dstPtr[dstIndex + 0] = srcPtr[srcIndex + 0]; // B
                                            dstPtr[dstIndex + 1] = srcPtr[srcIndex + 1]; // G
                                            dstPtr[dstIndex + 2] = srcPtr[srcIndex + 2]; // R
                                        }
                                    }
                                }
                                
                                // Unlock the bitmap
                                bmp.UnlockBits(bmpData);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to load image: {ex.Message}");
                    }

                    // Final check if the image was loaded successfully
                    if (originalImage.width <= 0 || originalImage.height <= 0)
                    {
                        throw new Exception("Failed to load the image. The file may be corrupted or in an unsupported format.");
                    }

                    // Initialize CoriginalImage as a copy of originalImage
                    CoriginalImage = cvlib.CvCreateImage(new CvSize(originalImage.width, originalImage.height), originalImage.depth, originalImage.nChannels);
                    cvlib.CvCopy(ref originalImage, ref CoriginalImage);

                    // Update the image in the PictureBox
                    UpdateImageOnResize(CoriginalImage);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}", "Image Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Clean up when the form is closed
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (originalImage.width > 0 && originalImage.height > 0)
            {
                cvlib.CvReleaseImage(ref originalImage);
            }
            if (CoriginalImage.width > 0 && CoriginalImage.height > 0)
            {
                cvlib.CvReleaseImage(ref CoriginalImage);
            }
            if (pictureBox1.Image != null)
            {
                pictureBox1.Image.Dispose();
            }
            base.OnFormClosed(e);
        }

        private void redToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Create a copy of the current image
                img = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), CoriginalImage.depth, CoriginalImage.nChannels);
                cvlib.CvCopy(ref CoriginalImage, ref img);

                // Access image data
                IntPtr srcData = img.imageData;
                IntPtr dstData = img.imageData;
                int step = img.widthStep; // Account for row alignment/padding

                unsafe
                {
                    byte* srcPtr = (byte*)srcData.ToPointer();
                    byte* dstPtr = (byte*)dstData.ToPointer();

                    for (int r = 0; r < img.height; r++)
                    {
                        for (int c = 0; c < img.width; c++)
                        {
                            int index = (r * step) + (c * img.nChannels); // Use widthStep for row offset
                            dstPtr[index + 0] = 0; // Blue
                            dstPtr[index + 1] = 0; // Green
                            dstPtr[index + 2] = srcPtr[index + 2]; // Red
                        }
                    }
                }

                // Update CoriginalImage to the converted image
                CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary img
                cvlib.CvReleaseImage(ref img);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing red channel: {ex.Message}");
            }
        }

        private void greenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Create a copy of the current image
                img = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), CoriginalImage.depth, CoriginalImage.nChannels);
                cvlib.CvCopy(ref CoriginalImage, ref img);

                // Access image data
                IntPtr srcData = img.imageData;
                IntPtr dstData = img.imageData;
                int step = img.widthStep; // Account for row alignment/padding

                unsafe
                {
                    byte* srcPtr = (byte*)srcData.ToPointer();
                    byte* dstPtr = (byte*)dstData.ToPointer();

                    for (int r = 0; r < img.height; r++)
                    {
                        for (int c = 0; c < img.width; c++)
                        {
                            int index = (r * step) + (c * img.nChannels); // Use widthStep for row offset
                            dstPtr[index + 0] = 0; // Blue
                            dstPtr[index + 1] = srcPtr[index + 1]; // Green
                            dstPtr[index + 2] = 0; // Red
                        }
                    }
                }

                // Update CoriginalImage to the converted image
                CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary img
                cvlib.CvReleaseImage(ref img);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing green channel: {ex.Message}");
            }
        }

        private void blueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Create a copy of the current image
                img = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), CoriginalImage.depth, CoriginalImage.nChannels);
                cvlib.CvCopy(ref CoriginalImage, ref img);

                // Access image data
                IntPtr srcData = img.imageData;
                IntPtr dstData = img.imageData;
                int step = img.widthStep; // Account for row alignment/padding

                unsafe
                {
                    byte* srcPtr = (byte*)srcData.ToPointer();
                    byte* dstPtr = (byte*)dstData.ToPointer();

                    for (int r = 0; r < img.height; r++)
                    {
                        for (int c = 0; c < img.width; c++)
                        {
                            int index = (r * step) + (c * img.nChannels); // Use widthStep for row offset
                            dstPtr[index + 0] = srcPtr[index + 0]; // Blue
                            dstPtr[index + 1] = 0; // Green
                            dstPtr[index + 2] = 0; // Red
                        }
                    }
                }

                // Update CoriginalImage to the converted image
                CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary img
                cvlib.CvReleaseImage(ref img);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing blue channel: {ex.Message}");
            }
        }

        private void grayscaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Create a copy of the current image
                img = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), CoriginalImage.depth, CoriginalImage.nChannels);
                cvlib.CvCopy(ref CoriginalImage, ref img);

                // Access image data
                IntPtr srcData = img.imageData;
                IntPtr dstData = img.imageData;
                int step = img.widthStep; // Account for row alignment/padding

                unsafe
                {
                    byte* srcPtr = (byte*)srcData.ToPointer();
                    byte* dstPtr = (byte*)dstData.ToPointer();

                    for (int r = 0; r < img.height; r++)
                    {
                        for (int c = 0; c < img.width; c++)
                        {
                            int index = (r * step) + (c * img.nChannels);
                            // Compute grayscale value: (R + G + B) / 3
                            byte gray = (byte)((srcPtr[index + 2] + srcPtr[index + 1] + srcPtr[index + 0]) / 3);
                            dstPtr[index + 0] = gray; // Blue
                            dstPtr[index + 1] = gray; // Green
                            dstPtr[index + 2] = gray; // Red
                        }
                    }
                }

                // Update CoriginalImage to the converted image
                CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary img
                cvlib.CvReleaseImage(ref img);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing grayscale: {ex.Message}");
            }
        }

        private void resetAllEditsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if an image is loaded
                if (originalImage.width <= 0 || originalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release previous CoriginalImage
                if (CoriginalImage.width > 0 && CoriginalImage.height > 0)
                {
                    cvlib.CvReleaseImage(ref CoriginalImage);
                }

                // Update CoriginalImage to match originalImage
                CoriginalImage = cvlib.CvCreateImage(new CvSize(originalImage.width, originalImage.height), originalImage.depth, originalImage.nChannels);
                cvlib.CvCopy(ref originalImage, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting edits: {ex.Message}");
            }
        }

        private void histogramToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Create and show the histogram form
                FormHistogram histogramForm = new FormHistogram(CoriginalImage);
                histogramForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying histogram: {ex.Message}");
            }
        }

        private void equalizeTheImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Create a copy of the current image (CoriginalImage instead of originalImage)
                img = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), CoriginalImage.depth, CoriginalImage.nChannels);
                cvlib.CvCopy(ref CoriginalImage, ref img);

                int width = img.width;
                int height = img.height;
                int step = img.widthStep;
                int nChannels = img.nChannels;

                // Step 1: Calculate N(i) - Histogram of each channel
                int[] ni_Red = new int[256];
                int[] ni_Green = new int[256];
                int[] ni_Blue = new int[256];

                IntPtr srcData = img.imageData;
                unsafe
                {
                    byte* srcPtr = (byte*)srcData.ToPointer();
                    for (int r = 0; r < height; r++)
                    {
                        for (int c = 0; c < width; c++)
                        {
                            int index = (r * step) + (c * nChannels);
                            ni_Blue[srcPtr[index + 0]]++;  // Blue
                            ni_Green[srcPtr[index + 1]]++; // Green
                            ni_Red[srcPtr[index + 2]]++;   // Red
                        }
                    }
                }

                // Step 2: Calculate P(Ni) - Probability of each intensity
                decimal[] prob_ni_Red = new decimal[256];
                decimal[] prob_ni_Green = new decimal[256];
                decimal[] prob_ni_Blue = new decimal[256];

                for (int i = 0; i < 256; i++)
                {
                    prob_ni_Red[i] = (decimal)ni_Red[i] / (decimal)(width * height);
                    prob_ni_Green[i] = (decimal)ni_Green[i] / (decimal)(width * height);
                    prob_ni_Blue[i] = (decimal)ni_Blue[i] / (decimal)(width * height);
                }

                // Step 3: Calculate CDF - Cumulative Distribution Function
                decimal[] cdf_Red = new decimal[256];
                decimal[] cdf_Green = new decimal[256];
                decimal[] cdf_Blue = new decimal[256];

                cdf_Red[0] = prob_ni_Red[0];
                cdf_Green[0] = prob_ni_Green[0];
                cdf_Blue[0] = prob_ni_Blue[0];

                for (int i = 1; i < 256; i++)
                {
                    cdf_Red[i] = prob_ni_Red[i] + cdf_Red[i - 1];
                    cdf_Green[i] = prob_ni_Green[i] + cdf_Green[i - 1];
                    cdf_Blue[i] = prob_ni_Blue[i] + cdf_Blue[i - 1];
                }

                // Step 4: Apply histogram equalization - CDF * (L-1)
                IntPtr dstData = img.imageData;
                const int constant = 255;

                unsafe
                {
                    byte* dstPtr = (byte*)dstData.ToPointer();
                    for (int r = 0; r < height; r++)
                    {
                        for (int c = 0; c < width; c++)
                        {
                            int index = (r * step) + (c * nChannels);
                            // Get original pixel values
                            byte blue = dstPtr[index + 0];
                            byte green = dstPtr[index + 1];
                            byte red = dstPtr[index + 2];

                            // Apply equalization
                            dstPtr[index + 0] = (byte)(cdf_Blue[blue] * constant);  // Blue
                            dstPtr[index + 1] = (byte)(cdf_Green[green] * constant); // Green
                            dstPtr[index + 2] = (byte)(cdf_Red[red] * constant);     // Red
                        }
                    }
                }

                // Update CoriginalImage to the equalized image
                CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary img
                cvlib.CvReleaseImage(ref img);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error equalizing image: {ex.Message}");
            }
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            // Gauss Blur 25%
            ApplyGaussianBlur(25);
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            // Gauss Blur 50%
            ApplyGaussianBlur(50);
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            // Gauss Blur 75%
            ApplyGaussianBlur(75);
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            // Gauss Blur 100%
            ApplyGaussianBlur(100);
        }
        
        private void ApplyGaussianBlur(int blurPercent)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Create a copy of the current image (CoriginalImage instead of originalImage)
                img = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), CoriginalImage.depth, CoriginalImage.nChannels);
                cvlib.CvCopy(ref CoriginalImage, ref img);

                // Convert percentage to kernel size (odd numbers from 3 to 15)
                int kernelSize = 3;
                if (blurPercent <= 25)
                    kernelSize = 3;
                else if (blurPercent <= 50)
                    kernelSize = 5;
                else if (blurPercent <= 75)
                    kernelSize = 9;
                else
                    kernelSize = 15;

                // Apply Gaussian blur
                cvlib.CvSmooth(ref img, ref img, cvlib.CV_GAUSSIAN, kernelSize, kernelSize, 0, 0);

                // Update CoriginalImage to the blurred image
                cvlib.CvReleaseImage(ref CoriginalImage);
                CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary img
                cvlib.CvReleaseImage(ref img);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying Gaussian blur: {ex.Message}");
            }
        }
        
        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            // Sobel Edge Detection 25%
            ApplySobelEdgeDetection(10);
        }

        private void toolStripMenuItem7_Click(object sender, EventArgs e)
        {
            // Sobel Edge Detection 50%
            ApplySobelEdgeDetection(15);
        }

        private void toolStripMenuItem8_Click(object sender, EventArgs e)
        {
            // Sobel Edge Detection 75%
            ApplySobelEdgeDetection(20);
        }

        private void toolStripMenuItem9_Click(object sender, EventArgs e)
        {
            // Sobel Edge Detection 100%
            ApplySobelEdgeDetection(25);
        }
        
        private void ApplySobelEdgeDetection(int edgePercent)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Log image properties
                Debug.WriteLine($"CoriginalImage: width={CoriginalImage.width}, height={CoriginalImage.height}, depth={CoriginalImage.depth}, nChannels={CoriginalImage.nChannels}");

                // Verify image is 3-channel BGR
                if (CoriginalImage.nChannels != 3)
                {
                    throw new Exception("Image must be a 3-channel BGR image.");
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Convert percentage to kernel size (odd numbers from 3 to 9)
                int kernelSize = 3;
                if (edgePercent <= 25)
                    kernelSize = 3;
                else if (edgePercent <= 50)
                    kernelSize = 5;
                else if (edgePercent <= 75)
                    kernelSize = 7;
                else
                    kernelSize = 9;

                // Create a grayscale copy of the current image
                IplImage grayImage = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), unchecked((int)cvlib.IPL_DEPTH_8U), 1);
                Debug.WriteLine($"grayImage created: width={grayImage.width}, height={grayImage.height}, depth={grayImage.depth}, nChannels={grayImage.nChannels}");
                cvlib.CvCvtColor(ref CoriginalImage, ref grayImage, cvlib.CV_BGR2GRAY);

                // Create temporary images for Sobel gradients
                IplImage gradX = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), unchecked((int)cvlib.IPL_DEPTH_16S), 1);
                IplImage gradY = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), unchecked((int)cvlib.IPL_DEPTH_16S), 1);
                IplImage absGradX = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), unchecked((int)cvlib.IPL_DEPTH_8U), 1);
                IplImage absGradY = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), unchecked((int)cvlib.IPL_DEPTH_8U), 1);
                
                // Create a single-channel destination image
                IplImage edgeImage = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), unchecked((int)cvlib.IPL_DEPTH_8U), 1);

                // Apply Sobel operator
                cvlib.CvSobel(ref grayImage, ref gradX, 1, 0, kernelSize);
                cvlib.CvSobel(ref grayImage, ref gradY, 0, 1, kernelSize);

                // Convert gradients to absolute values
                cvlib.CvConvertScaleAbs(ref gradX, ref absGradX, 1, 0);
                cvlib.CvConvertScaleAbs(ref gradY, ref absGradY, 1, 0);

                // Combine gradients (simple addition for edge magnitude)
                cvlib.CvAddWeighted(ref absGradX, 0.5, ref absGradY, 0.5, 0, ref edgeImage);

                // Convert single-channel edge map to 3-channel for PictureBox compatibility
                IplImage tempImage = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), unchecked((int)cvlib.IPL_DEPTH_8U), 3);
                cvlib.CvCvtColor(ref edgeImage, ref tempImage, cvlib.CV_GRAY2BGR);

                // Update CoriginalImage
                cvlib.CvReleaseImage(ref CoriginalImage);
                CoriginalImage = cvlib.CvCreateImage(new CvSize(tempImage.width, tempImage.height), tempImage.depth, tempImage.nChannels);
                cvlib.CvCopy(ref tempImage, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary images
                cvlib.CvReleaseImage(ref grayImage);
                cvlib.CvReleaseImage(ref gradX);
                cvlib.CvReleaseImage(ref gradY);
                cvlib.CvReleaseImage(ref absGradX);
                cvlib.CvReleaseImage(ref absGradY);
                cvlib.CvReleaseImage(ref edgeImage);
                cvlib.CvReleaseImage(ref tempImage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying Sobel edge detection: {ex.Message}");
            }
        }
        private void toolStripMenuItem10_Click(object sender, EventArgs e)
        {
            // Brightness Adjustment 25%
            ApplyBrightnessAdjustment(10);
        }

        private void toolStripMenuItem11_Click(object sender, EventArgs e)
        {
            // Brightness Adjustment 50%
            ApplyBrightnessAdjustment(15);
        }

        private void toolStripMenuItem12_Click(object sender, EventArgs e)
        {
            // Brightness Adjustment 75%
            ApplyBrightnessAdjustment(20);
        }

        private void toolStripMenuItem13_Click(object sender, EventArgs e)
        {
            // Brightness Adjustment 100%
            ApplyBrightnessAdjustment(25);
        }

        private void ApplyBrightnessAdjustment(int brightnessPercent)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Create a copy of the current image
                img = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), CoriginalImage.depth, CoriginalImage.nChannels);
                cvlib.CvCopy(ref CoriginalImage, ref img);

                // Calculate brightness adjustment value (0-255)
                int brightnessValue = (brightnessPercent * 255) / 100;

                // Access image data
                IntPtr srcData = img.imageData;
                IntPtr dstData = img.imageData;
                int step = img.widthStep; // Account for row alignment/padding

                unsafe
                {
                    byte* srcPtr = (byte*)srcData.ToPointer();
                    byte* dstPtr = (byte*)dstData.ToPointer();

                    for (int r = 0; r < img.height; r++)
                    {
                        for (int c = 0; c < img.width; c++)
                        {
                            int index = (r * step) + (c * img.nChannels);
                            
                            // Adjust each channel with brightness value
                            // Ensure values stay within 0-255 range
                            dstPtr[index + 0] = (byte)Math.Min(255, Math.Max(0, srcPtr[index + 0] + brightnessValue)); // Blue
                            dstPtr[index + 1] = (byte)Math.Min(255, Math.Max(0, srcPtr[index + 1] + brightnessValue)); // Green
                            dstPtr[index + 2] = (byte)Math.Min(255, Math.Max(0, srcPtr[index + 2] + brightnessValue)); // Red
                        }
                    }
                }

                // Update CoriginalImage to the brightness-adjusted image
                cvlib.CvReleaseImage(ref CoriginalImage);
                CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary img
                cvlib.CvReleaseImage(ref img);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adjusting brightness: {ex.Message}");
            }
        }

        private void toolStripMenuItem14_Click(object sender, EventArgs e)
        {
            // Negative Filter 25%
            ApplyNegativeFilter(10);
        }

        private void toolStripMenuItem15_Click(object sender, EventArgs e)
        {
            // Negative Filter 50%
            ApplyNegativeFilter(15);
        }

        private void toolStripMenuItem16_Click(object sender, EventArgs e)
        {
            // Negative Filter 75%
            ApplyNegativeFilter(20);
        }

        private void toolStripMenuItem17_Click(object sender, EventArgs e)
        {
            // Negative Filter 100%
            ApplyNegativeFilter(25);
        }

        private void ApplyNegativeFilter(int negativePercent)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Create a copy of the current image
                img = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), CoriginalImage.depth, CoriginalImage.nChannels);
                cvlib.CvCopy(ref CoriginalImage, ref img);

                // Access image data
                IntPtr srcData = img.imageData;
                IntPtr dstData = img.imageData;
                int step = img.widthStep; // Account for row alignment/padding

                unsafe
                {
                    byte* srcPtr = (byte*)srcData.ToPointer();
                    byte* dstPtr = (byte*)dstData.ToPointer();

                    for (int r = 0; r < img.height; r++)
                    {
                        for (int c = 0; c < img.width; c++)
                        {
                            int index = (r * step) + (c * img.nChannels);
                            
                            // Calculate negative effect based on percentage
                            // For each channel, blend between original and negative (255 - value)
                            float blendFactor = negativePercent / 100.0f;
                            
                            // Blue channel
                            byte originalBlue = srcPtr[index + 0];
                            byte negativeBlue = (byte)(255 - originalBlue);
                            dstPtr[index + 0] = (byte)((originalBlue * (1 - blendFactor)) + (negativeBlue * blendFactor));
                            
                            // Green channel
                            byte originalGreen = srcPtr[index + 1];
                            byte negativeGreen = (byte)(255 - originalGreen);
                            dstPtr[index + 1] = (byte)((originalGreen * (1 - blendFactor)) + (negativeGreen * blendFactor));
                            
                            // Red channel
                            byte originalRed = srcPtr[index + 2];
                            byte negativeRed = (byte)(255 - originalRed);
                            dstPtr[index + 2] = (byte)((originalRed * (1 - blendFactor)) + (negativeRed * blendFactor));
                        }
                    }
                }

                // Update CoriginalImage to the negative-filtered image
                cvlib.CvReleaseImage(ref CoriginalImage);
                CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary img
                cvlib.CvReleaseImage(ref img);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying negative filter: {ex.Message}");
            }
        }

                private void antiClockwiseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Rotate 90 degrees counter-clockwise
            ApplyRotation(false);
        }

        private void clockwiseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Rotate 90 degrees clockwise
            ApplyRotation(true);
        }

        private void ApplyRotation(bool clockwise)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Create a new image with swapped dimensions for 90-degree rotation
                img = cvlib.CvCreateImage(new CvSize(CoriginalImage.height, CoriginalImage.width), CoriginalImage.depth, CoriginalImage.nChannels);

                // Apply transpose operation
                cvlib.CvTranspose(ref CoriginalImage, ref img);
                
                // Create a temporary image for the flip operation
                IplImage tempImg = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref tempImg);
                
                // Flip the image based on rotation direction
                if (clockwise)
                {
                    // For clockwise: transpose then flip around y-axis (horizontal flip)
                    cvlib.CvFlip(ref tempImg, ref img, 1); // 1 = horizontal flip (around y-axis)
                }
                else
                {
                    // For counter-clockwise: transpose then flip around x-axis (vertical flip)
                    cvlib.CvFlip(ref tempImg, ref img, 0); // 0 = vertical flip (around x-axis)
                }

                // Release the temporary image
                cvlib.CvReleaseImage(ref tempImg);

                // Update CoriginalImage to the rotated image
                cvlib.CvReleaseImage(ref CoriginalImage);
                CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary img
                cvlib.CvReleaseImage(ref img);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rotating image: {ex.Message}");
            }
        }

        private void horizontallyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Flip image horizontally
            ApplyFlip(true);
        }

        private void verticallyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Flip image vertically
            ApplyFlip(false);
        }

        private void ApplyFlip(bool horizontal)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Release any previously loaded img to free memory
                if (img.width > 0 && img.height > 0)
                {
                    cvlib.CvReleaseImage(ref img);
                }

                // Create a copy of the current image
                img = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), CoriginalImage.depth, CoriginalImage.nChannels);
                cvlib.CvCopy(ref CoriginalImage, ref img);

                // Apply flip operation
                // 1 = horizontal flip (around y-axis)
                // 0 = vertical flip (around x-axis)
                int flipCode = horizontal ? 1 : 0;
                cvlib.CvFlip(ref img, ref img, flipCode);

                // Update CoriginalImage to the flipped image
                cvlib.CvReleaseImage(ref CoriginalImage);
                CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                cvlib.CvCopy(ref img, ref CoriginalImage);

                // Update the image in the PictureBox
                UpdateImageOnResize(CoriginalImage);

                // Release temporary img
                cvlib.CvReleaseImage(ref img);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error flipping image: {ex.Message}");
            }
        }

        private void toolStripMenuItem18_Click(object sender, EventArgs e)
        {
            // Compress 25%
            ApplyCompression(25);
        }

        private void toolStripMenuItem19_Click(object sender, EventArgs e)
        {
            // Compress 50%
            ApplyCompression(50);
        }

        private void toolStripMenuItem20_Click(object sender, EventArgs e)
        {
            // Compress 75%
            ApplyCompression(75);
        }

        private void ApplyCompression(int compressionPercent)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Create temporary Bitmap for compression
                using (Bitmap bmp = new Bitmap(CoriginalImage.width, CoriginalImage.height))
                {
                    // Convert IplImage to Bitmap
                    Rectangle rect = new Rectangle(0, 0, CoriginalImage.width, CoriginalImage.height);
                    BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                    unsafe
                    {
                        byte* bmpPtr = (byte*)bmpData.Scan0.ToPointer();
                        byte* imgPtr = (byte*)CoriginalImage.imageData.ToPointer();

                        for (int y = 0; y < CoriginalImage.height; y++)
                        {
                            for (int x = 0; x < CoriginalImage.width; x++)
                            {
                                int imgIndex = y * CoriginalImage.widthStep + x * 3;
                                int bmpIndex = y * bmpData.Stride + x * 3;

                                bmpPtr[bmpIndex + 0] = imgPtr[imgIndex + 0]; // Blue
                                bmpPtr[bmpIndex + 1] = imgPtr[imgIndex + 1]; // Green
                                bmpPtr[bmpIndex + 2] = imgPtr[imgIndex + 2]; // Red
                            }
                        }
                    }

                    bmp.UnlockBits(bmpData);

                    // Create EncoderParameters for JPEG compression
                    EncoderParameters encoderParams = new EncoderParameters(1);
                    // Convert compression percentage to quality (inverse relationship)
                    long quality = 100 - compressionPercent;
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

                    // Get JPEG codec info
                    ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");

                    // Save compressed image to memory stream
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, jpegCodec, encoderParams);
                        
                        // Load compressed image back
                        ms.Position = 0;
                        using (Bitmap compressedBmp = new Bitmap(ms))
                        {
                            // Convert back to IplImage
                            Rectangle compRect = new Rectangle(0, 0, compressedBmp.Width, compressedBmp.Height);
                            BitmapData compData = compressedBmp.LockBits(compRect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                            // Release previous images
                            if (img.width > 0 && img.height > 0)
                            {
                                cvlib.CvReleaseImage(ref img);
                            }

                            // Create new IplImage
                            img = cvlib.CvCreateImage(new CvSize(CoriginalImage.width, CoriginalImage.height), CoriginalImage.depth, CoriginalImage.nChannels);

                            unsafe
                            {
                                byte* compPtr = (byte*)compData.Scan0.ToPointer();
                                byte* destPtr = (byte*)img.imageData.ToPointer();

                                for (int y = 0; y < img.height; y++)
                                {
                                    for (int x = 0; x < img.width; x++)
                                    {
                                        int compIndex = y * compData.Stride + x * 3;
                                        int destIndex = y * img.widthStep + x * 3;

                                        destPtr[destIndex + 0] = compPtr[compIndex + 0]; // Blue
                                        destPtr[destIndex + 1] = compPtr[compIndex + 1]; // Green
                                        destPtr[destIndex + 2] = compPtr[compIndex + 2]; // Red
                                    }
                                }
                            }

                            compressedBmp.UnlockBits(compData);

                            // Update CoriginalImage
                            cvlib.CvReleaseImage(ref CoriginalImage);
                            CoriginalImage = cvlib.CvCreateImage(new CvSize(img.width, img.height), img.depth, img.nChannels);
                            cvlib.CvCopy(ref img, ref CoriginalImage);

                            // Update the image in the PictureBox
                            UpdateImageOnResize(CoriginalImage);

                            // Release temporary img
                            cvlib.CvReleaseImage(ref img);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error compressing image: {ex.Message}");
            }
        }

        private ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            return codecs.FirstOrDefault(codec => codec.MimeType == mimeType);
        }

        private void metadataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Create and show the metadata form
                Form2 metadataForm = new Form2();
                metadataForm.Text = "Image Metadata";
                
                // Get the path of the currently loaded image
                // Assuming you have stored the original image path somewhere
                // You might need to add a class-level variable to store this
                if (string.IsNullOrEmpty(currentImagePath))
                {
                    MessageBox.Show("Image path not available.");
                    return;
                }

                metadataForm.DisplayMetadata(currentImagePath);
                metadataForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying metadata: {ex.Message}");
            }
        }


        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About about1 = new About();
            about1.Show();
        }

        private void pNGToolStripMenuItem_Click(object sender, EventArgs e)
        {// Export to png
            try
            {
                // Check if an image is loaded
                if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
                {
                    MessageBox.Show("No image loaded. Please open an image first.");
                    return;
                }

                // Configure save file dialog
                pngsave.Filter = "PNG Image|*.png";
                pngsave.Title = "Save Image as PNG";
                pngsave.DefaultExt = "png";

                // Set default filename with _edited suffix
                if (!string.IsNullOrEmpty(currentImagePath))
                {
                    string originalFileName = Path.GetFileNameWithoutExtension(currentImagePath);
                    string defaultFileName = $"{originalFileName}_edited.png";
                    pngsave.FileName = defaultFileName;
                }

                if (pngsave.ShowDialog() == DialogResult.OK)
                {
                    // Create a Bitmap from the current image
                    using (Bitmap bmp = new Bitmap(CoriginalImage.width, CoriginalImage.height))
                    {
                        // Lock the bitmap's bits
                        Rectangle rect = new Rectangle(0, 0, CoriginalImage.width, CoriginalImage.height);
                        BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                        unsafe
                        {
                            byte* bmpPtr = (byte*)bmpData.Scan0.ToPointer();
                            byte* imgPtr = (byte*)CoriginalImage.imageData.ToPointer();

                            for (int y = 0; y < CoriginalImage.height; y++)
                            {
                                for (int x = 0; x < CoriginalImage.width; x++)
                                {
                                    int imgIndex = y * CoriginalImage.widthStep + x * 3;
                                    int bmpIndex = y * bmpData.Stride + x * 3;

                                    // Copy BGR to RGB (keep original order since we're using CoriginalImage)
                                    bmpPtr[bmpIndex + 0] = imgPtr[imgIndex + 0]; // B
                                    bmpPtr[bmpIndex + 1] = imgPtr[imgIndex + 1]; // G
                                    bmpPtr[bmpIndex + 2] = imgPtr[imgIndex + 2]; // R
                                }
                            }
                        }

                        // Unlock the bits
                        bmp.UnlockBits(bmpData);

                        // Save as PNG
                        bmp.Save(pngsave.FileName, ImageFormat.Png);
                    }

                    MessageBox.Show("Image saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving PNG: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

private void jPGToolStripMenuItem_Click(object sender, EventArgs e)
{
    SaveAsJpg(false); // false for .jpg extension
}

private void jPEGToolStripMenuItem_Click(object sender, EventArgs e)
{
    SaveAsJpg(true); // true for .jpeg extension
}

private void SaveAsJpg(bool useJpegExtension)
{
    try
    {
        // Check if an image is loaded
        if (CoriginalImage.width <= 0 || CoriginalImage.height <= 0)
        {
            MessageBox.Show("No image loaded. Please open an image first.");
            return;
        }

        // Configure save file dialog
        string ext = useJpegExtension ? "jpeg" : "jpg";
        pngsave.Filter = $"JPEG Image|*.{ext}";
        pngsave.Title = $"Save Image as {ext.ToUpper()}";
        pngsave.DefaultExt = ext;

        // Set default filename with _edited suffix
        if (!string.IsNullOrEmpty(currentImagePath))
        {
            string originalFileName = Path.GetFileNameWithoutExtension(currentImagePath);
            string defaultFileName = $"{originalFileName}_edited.{ext}";
            pngsave.FileName = defaultFileName;
        }

        if (pngsave.ShowDialog() == DialogResult.OK)
        {
            // Create a Bitmap from the current image
            using (Bitmap bmp = new Bitmap(CoriginalImage.width, CoriginalImage.height))
            {
                // Lock the bitmap's bits
                Rectangle rect = new Rectangle(0, 0, CoriginalImage.width, CoriginalImage.height);
                BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                unsafe
                {
                    byte* bmpPtr = (byte*)bmpData.Scan0.ToPointer();
                    byte* imgPtr = (byte*)CoriginalImage.imageData.ToPointer();

                    for (int y = 0; y < CoriginalImage.height; y++)
                    {
                        for (int x = 0; x < CoriginalImage.width; x++)
                        {
                            int imgIndex = y * CoriginalImage.widthStep + x * 3;
                            int bmpIndex = y * bmpData.Stride + x * 3;

                            bmpPtr[bmpIndex + 0] = imgPtr[imgIndex + 0]; // B
                            bmpPtr[bmpIndex + 1] = imgPtr[imgIndex + 1]; // G
                            bmpPtr[bmpIndex + 2] = imgPtr[imgIndex + 2]; // R
                        }
                    }
                }

                bmp.UnlockBits(bmpData);

                // Create encoder parameters for JPEG (default quality)
                EncoderParameters encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 90L);

                // Get JPEG codec info
                ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");

                // Save as JPEG
                bmp.Save(pngsave.FileName, jpegCodec, encoderParams);
            }

            MessageBox.Show("Image saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error saving {(useJpegExtension ? "JPEG" : "JPG")}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

        private void gaussianBlurToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}