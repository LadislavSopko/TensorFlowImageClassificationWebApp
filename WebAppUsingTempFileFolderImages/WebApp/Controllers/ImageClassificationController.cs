﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ML;
using WebApp.Infrastructure;
using WebApp.ML.DataModels;

namespace WebApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageClassificationController : ControllerBase
    {
        public IConfiguration Configuration { get; }
        private readonly PredictionEnginePool<ImageInputData, ImageLabelPredictions> _predictionEnginePool;
        private readonly ILogger<ImageClassificationController> _logger;

        //(CDLTLL-TODO)
        private readonly string _tempImagesFolderPath;
        private readonly string _labelsFilePath;

        //DELETE
        //private readonly string _tempImagesFolderPath = GetAbsolutePath(@"TempImages");
        //private readonly string _labelsFilePath = GetAbsolutePath(@"ML/TensorFlowModel/labels.txt");

        public ImageClassificationController(PredictionEnginePool<ImageInputData, ImageLabelPredictions> predictionEnginePool, IConfiguration configuration, ILogger<ImageClassificationController> logger) //When using DI/IoC
        {
            // Get the ML Model Engine injected, for scoring
            _predictionEnginePool = predictionEnginePool;

            Configuration = configuration;
            _labelsFilePath = GetAbsolutePath(Configuration["MLModel:LabelsFilePath"]);
            _tempImagesFolderPath = GetAbsolutePath(Configuration["MLModel:TempImagesFolderPath"]);

            //Get other injected dependencies
            _logger = logger;
        }

        [HttpPost]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [Route("classifyimage")]
        public async Task<IActionResult> ClassifyImage(IFormFile imageFile)
        {
            if (imageFile.Length == 0)
                return BadRequest();

            //Save the temp image file into the temp-folder 
            IImageFileWriter imageWriter = new ImageFileWriter();
            string fileName = await imageWriter.UploadImageAsync(imageFile, _tempImagesFolderPath);
            string imageFilePath = Path.Combine(_tempImagesFolderPath, fileName);

            //Convert image stream to byte[] 
            //
            //byte[] imageData = null;

            //MemoryStream image = new MemoryStream();
            //await imageFile.CopyToAsync(image);
            //imageData = image.ToArray();
            //if (!imageData.IsValidImage())
            //    return StatusCode(StatusCodes.Status415UnsupportedMediaType);

            _logger.LogInformation($"Start processing image...");

            //Measure execution time
            var watch = System.Diagnostics.Stopwatch.StartNew();

            //Set the specific image data
            var imageInputData = new ImageInputData { ImagePath = imageFilePath };

            //Predict code for provided image
            ImageLabelPredictions imageLabelPredictions = _predictionEnginePool.Predict(imageInputData);

            //Stop measuring time
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            _logger.LogInformation($"Image processed in {elapsedMs} miliseconds");

            //try
            //{
            //    // DELETE FILE WHEN CLOSED
            //    using (FileStream fs = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose))
            //    {
            //        // temp file exists
            //        fs.Close();
            //    }
            //}
            //catch (Exception e)
            //{
            //    throw e;
            //}

            //Predict the image's label (The one with highest probability)
            ImagePredictedLabelWithProbability imageBestLabelPrediction  
                                = FindBestLabelWithProbability(imageLabelPredictions, imageInputData);

            //return new ObjectResult(result);
            return Ok(imageBestLabelPrediction);
        }

        private ImagePredictedLabelWithProbability FindBestLabelWithProbability(ImageLabelPredictions imageLabelPredictions, ImageInputData imageInputData)
        {
            //Read TF model's labels (labels.txt) to classify the image across those labels
            var labels = ReadLabels(_labelsFilePath);

            float[] probabilities = imageLabelPredictions.PredictedLabels;

            //Set a single label as predicted or even none if probabilities were lower than 70%
            var imageBestLabelPrediction = new ImagePredictedLabelWithProbability()
            {
                ImagePath = imageInputData.ImagePath,
            };

            (imageBestLabelPrediction.PredictedLabel, imageBestLabelPrediction.Probability) = GetBestLabel(labels, probabilities);

            return imageBestLabelPrediction;
        }

        private (string, float) GetBestLabel(string[] labels, float[] probs)
        {
            var max = probs.Max();
            var index = probs.AsSpan().IndexOf(max);

            if (max > 0.7)
                return (labels[index], max);
            else
                return ("None", max);
        }

        private string[] ReadLabels(string labelsLocation)
        {
            return System.IO.File.ReadAllLines(labelsLocation);
        }

        public static string GetAbsolutePath(string relativePath)
        {
            FileInfo _dataRoot = new FileInfo(typeof(Program).Assembly.Location);
            string assemblyFolderPath = _dataRoot.Directory.FullName;

            string fullPath = Path.Combine(assemblyFolderPath, relativePath);
            return fullPath;
        }

        // GET api/ImageClassification
        [HttpGet]
        public ActionResult<IEnumerable<string>> Get()
        {
            return new string[] { "ACK Heart beat 1", "ACK Heart beat 2" };
        }
    }
}