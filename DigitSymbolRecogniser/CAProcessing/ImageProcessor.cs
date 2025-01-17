﻿//
//  THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
//  KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
//  IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR
//  PURPOSE.
//
//  License: GNU General Public License version 3 (GPLv3)
//
//  Email: pavel_torgashov@mail.ru.
//
//  Copyright (C) Pavel Torgashov, 2011. 

using System;
using System.Collections.Generic;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Drawing;
using System.Threading.Tasks;
using Emgu.CV.CvEnum;

namespace ContourAnalysisNS
{
    public class ImageProcessor
    {
        //settings
        public bool equalizeHist = false;
        public bool noiseFilter = false;
        public int cannyThreshold = 50;
        public bool blur = true;
        public int adaptiveThresholdBlockSize = 4;
        public double adaptiveThresholdParameter = 1.2d;
        public bool addCanny = true;
        public bool filterContoursBySize = true;
        public bool onlyFindContours = false;
        public int minContourLength = 15;
        public int minContourArea = 10;
        public double minFormFactor = 0.5;
        //
        public List<Contour<Point>> contours;
        public Templates templates = new Templates();
        public Templates samples = new Templates();
        public List<FoundTemplateDesc> foundTemplates = new List<FoundTemplateDesc>();
        public TemplateFinder finder = new TemplateFinder();
        public Image<Gray, byte> binarizedFrame;
        

        public void ProcessImage(Image<Bgr, byte> frame, bool enableMaxContour = false)
        {
            ProcessImage(frame.Convert<Gray, Byte>(), enableMaxContour);
        }

        public void ProcessImage(Image<Gray, byte> grayFrame, bool enableMaxContour = false)
        {
            if (equalizeHist)
                grayFrame._EqualizeHist();//autocontrast
            //smoothed
            Image<Gray, byte> smoothedGrayFrame = grayFrame.PyrDown();
            smoothedGrayFrame = smoothedGrayFrame.PyrUp();
            //canny
            Image<Gray, byte> cannyFrame = null;
            if (noiseFilter)
                cannyFrame = smoothedGrayFrame.Canny(new Gray(cannyThreshold), new Gray(cannyThreshold));
            //smoothing
            if (blur)
                grayFrame = smoothedGrayFrame;
            //binarize
            CvInvoke.cvAdaptiveThreshold(grayFrame, grayFrame, 255, ADAPTIVE_THRESHOLD_TYPE.CV_ADAPTIVE_THRESH_MEAN_C, THRESH.CV_THRESH_BINARY, 
                adaptiveThresholdBlockSize + adaptiveThresholdBlockSize % 2 + 1, adaptiveThresholdParameter);
            //
            grayFrame._Not();
            //
            if (addCanny)
            if (cannyFrame != null)
                grayFrame._Or(cannyFrame);
            //
            this.binarizedFrame = grayFrame;

            //dilate canny contours for filtering
            if (cannyFrame != null)
                cannyFrame = cannyFrame.Dilate(3);

            //find contours
            var sourceContours = grayFrame.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_NONE, RETR_TYPE.CV_RETR_LIST);
            //filter contours
            contours = FilterContours(sourceContours, cannyFrame, grayFrame.Width, grayFrame.Height, enableMaxContour);
            //contours = ConvertContours(sourceContours);
            foundTemplates = FindTemplates(contours);
        }

        public List<FoundTemplateDesc> FindTemplates(List<Contour<Point>> inputContours)
        {
            List<FoundTemplateDesc> foundTemplatesInput = new List<FoundTemplateDesc>();

            //find templates
            lock (foundTemplatesInput)
                foundTemplatesInput.Clear();
            samples.Clear();

            lock (templates)
                Parallel.ForEach(inputContours, contour =>
                {
                    var arr = contour.ToArray();
                    Template sample = new Template(arr, contour.Area, samples.templateSize);
                    //sample.name = 
                    lock (samples)
                        samples.Add(sample);

                    if (!onlyFindContours)
                    {
                        FoundTemplateDesc desc = finder.FindTemplate(templates, sample);

                        if (desc != null)
                            lock (foundTemplatesInput)
                                foundTemplatesInput.Add(desc);
                    }
                }
                    );
            //
            FilterByIntersection(ref foundTemplatesInput);

            return foundTemplatesInput;
        }

        public List<FoundTemplateDesc> FindTemplatesNonParalel(List<Contour<Point>> inputContours, bool detailed = false, bool rather6 = false, bool rather8 = false, bool rather9 = false, bool rather0 = false)
        {
            List<FoundTemplateDesc> foundTemplatesInput = new List<FoundTemplateDesc>();

            //find templates
            foundTemplatesInput.Clear();
            samples.Clear();

            foreach (var contour in inputContours)
            {
                var arr = contour.ToArray();
                    Template sample = new Template(arr, contour.Area, samples.templateSize);
                    samples.Add(sample);

                    if (!onlyFindContours)
                    {
                        FoundTemplateDesc desc = detailed ? finder.FindTemplateByNorma(templates, sample, rather6, rather8, rather9, rather0) : finder.FindTemplate(templates, sample);
                        //if (desc != null)
                        foundTemplatesInput.Add(desc);
                    }
            }

            //FilterByIntersectionWithNull(ref foundTemplatesInput);

            return foundTemplatesInput;
        }

        private static void FilterByIntersectionWithNull(ref List<FoundTemplateDesc> templates)
        {
            var toDel = GetTemplatesToDel(templates);
            List<FoundTemplateDesc> newTemplates = new List<FoundTemplateDesc>();
            for (int i = 0; i < templates.Count; i++)
            {
                if (!toDel.Contains(i))
                {
                    newTemplates.Add(templates[i]);
                }
                else
                {
                    newTemplates.Add(null);
                }
            }
            templates = newTemplates;
        }

        private static HashSet<int> GetTemplatesToDel(List<FoundTemplateDesc> templates)
        {
            //sort by area
            templates.Sort(
                ((t1, t2) => -t1.sample.contour.SourceBoundingRect.Area().CompareTo(t2.sample.contour.SourceBoundingRect.Area())));
            //exclude templates inside other templates
            HashSet<int> toDel = new HashSet<int>();
            for (int i = 0; i < templates.Count; i++)
            {
                if (toDel.Contains(i))
                    continue;
                Rectangle bigRect = templates[i].sample.contour.SourceBoundingRect;
                int bigArea = templates[i].sample.contour.SourceBoundingRect.Area();
                bigRect.Inflate(4, 4);
                for (int j = i + 1; j < templates.Count; j++)
                {
                    if (bigRect.Contains(templates[j].sample.contour.SourceBoundingRect))
                    {
                        double a = templates[j].sample.contour.SourceBoundingRect.Area();
                        if (a/bigArea > 0.9d)
                        {
                            //choose template by rate
                            if (templates[i].rate > templates[j].rate)
                                toDel.Add(j);
                            else
                                toDel.Add(i);
                        }
                        else //delete tempate
                            toDel.Add(j);
                    }
                }
            }
            return toDel;
        }

        private static void FilterByIntersection(ref List<FoundTemplateDesc> templates)
        {
            var toDel = GetTemplatesToDel(templates);
            List<FoundTemplateDesc> newTemplates = new List<FoundTemplateDesc>();
            for (int i = 0; i < templates.Count; i++)
                if (!toDel.Contains(i))
                    newTemplates.Add(templates[i]);
            templates = newTemplates;
        }

        private List<Contour<Point>> ConvertContours(Contour<Point> contours)
        {
            var c = contours;
            List<Contour<Point>> result = new List<Contour<Point>>();
            while (c != null)
            {
                result.Add(c);
                c = c.HNext;
            }
            return result;
        }

        private List<Contour<Point>> FilterContours(Contour<Point> contours, Image<Gray, byte> cannyFrame, 
            int frameWidth, int frameHeight, bool enableBigContour = false)
        {
            int maxArea = enableBigContour ? frameWidth * frameHeight / 2  : frameWidth * frameHeight / 5;
            var c = contours;
            List<Contour<Point>> result = new List<Contour<Point>>();

            while (c != null)
            {
                if (filterContoursBySize)
                    if (c.Total < minContourLength ||
                        c.Area < minContourArea ||
                        c.Area > maxArea ||
                        c.Area / c.Total <= minFormFactor)
                        goto next;

                if (noiseFilter)
                {
                    Point p1 = c[0];
                    Point p2 = c[(c.Total / 2) % c.Total];
                    if (cannyFrame[p1].Intensity <= double.Epsilon && cannyFrame[p2].Intensity <= double.Epsilon)
                        goto next;
                }
                result.Add(c);

            next:
                c = c.HNext;
            }

            return result;
        }
    }
}
