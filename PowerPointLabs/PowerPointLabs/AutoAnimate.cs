﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using PowerPointLabs.Models;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace PowerPointLabs
{
    class AutoAnimate
    {
        public static float defaultDuration = 0.5f;
        public static bool frameAnimationChecked = false;

        private static PowerPoint.Shape[] currentSlideShapes;
        private static PowerPoint.Shape[] nextSlideShapes;
        private static int[] matchingShapeIDs;

        public static void AddAutoAnimation()
        {
            try
            {
                //Get References of current and next slides
                var currentSlide = PowerPointPresentation.CurrentSlide as PowerPointSlide;
                if (currentSlide == null || currentSlide.Index == PowerPointPresentation.SlideCount)
                {
                   System.Windows.Forms.MessageBox.Show("Please select the correct slide", "Unable to Add Animations");
                   return;
                }

                PowerPointSlide nextSlide = PowerPointPresentation.Slides.ElementAt(currentSlide.Index);
                if (!GetMatchingShapeDetails(currentSlide, nextSlide))
                {
                    System.Windows.Forms.MessageBox.Show("No matching Shapes were found on the next slide", "Animation Not Added");
                    return;
                }

                AddCompleteAnimations(currentSlide, nextSlide);           
            }
            catch (Exception e)
            {
                //LogException(e, "AddAnimationButtonClick");
                throw;
            }
            
        }

        public static void ReloadAutoAnimation()
        {
            try
            {
                var selectedSlide = PowerPointPresentation.CurrentSlide as PowerPointSlide;
                PowerPointSlide currentSlide = null, animatedSlide = null, nextSlide = null;

                if (selectedSlide.Name.StartsWith("PPTAutoAnimateSlideAnimated"))
                {
                    nextSlide = PowerPointPresentation.Slides.ElementAt(selectedSlide.Index);
                    currentSlide = PowerPointPresentation.Slides.ElementAt(selectedSlide.Index - 2);
                    animatedSlide = selectedSlide;
                    ManageSlidesForReload(currentSlide, nextSlide, animatedSlide);
                }
                else if (selectedSlide.Name.StartsWith("PPTAutoAnimateSlideStart"))
                {
                    animatedSlide = PowerPointPresentation.Slides.ElementAt(selectedSlide.Index);
                    nextSlide = PowerPointPresentation.Slides.ElementAt(selectedSlide.Index + 1);
                    currentSlide = selectedSlide;
                    ManageSlidesForReload(currentSlide, nextSlide, animatedSlide);
                }
                else if (selectedSlide.Name.StartsWith("PPTAutoAnimateSlideEnd"))
                {
                    animatedSlide = PowerPointPresentation.Slides.ElementAt(selectedSlide.Index - 2);
                    currentSlide = PowerPointPresentation.Slides.ElementAt(selectedSlide.Index - 3);
                    nextSlide = selectedSlide;
                    ManageSlidesForReload(currentSlide, nextSlide, animatedSlide);
                }
                else if (selectedSlide.Name.StartsWith("PPTAutoAnimateSlideMulti"))
                {
                    if (selectedSlide.Index > 2)
                    {
                        animatedSlide = PowerPointPresentation.Slides.ElementAt(selectedSlide.Index - 2);
                        currentSlide = PowerPointPresentation.Slides.ElementAt(selectedSlide.Index - 3);
                        nextSlide = selectedSlide;
                        if (animatedSlide.Name.StartsWith("PPTAutoAnimateSlideAnimated"))
                            ManageSlidesForReload(currentSlide, nextSlide, animatedSlide);
                    }

                    if (selectedSlide.Index < PowerPointPresentation.SlideCount - 1)
                    {
                        animatedSlide = PowerPointPresentation.Slides.ElementAt(selectedSlide.Index);
                        nextSlide = PowerPointPresentation.Slides.ElementAt(selectedSlide.Index + 1);
                        currentSlide = selectedSlide;
                        if (animatedSlide.Name.StartsWith("PPTAutoAnimateSlideAnimated"))
                            ManageSlidesForReload(currentSlide, nextSlide, animatedSlide);
                    }
                }
                else
                    System.Windows.Forms.MessageBox.Show("The current slide was not added by PowerPointLabs Auto Animate", "Error");
            }
            catch (Exception e)
            {
                //LogException(e, "AddAnimationButtonClick");
                throw;
            }
        }

        private static void ManageSlidesForReload(PowerPointSlide currentSlide, PowerPointSlide nextSlide, PowerPointSlide animatedSlide)
        {
            animatedSlide.Delete();
            if (!GetMatchingShapeDetails(currentSlide, nextSlide))
            {
                System.Windows.Forms.MessageBox.Show("No matching Shapes were found on the next slide", "Animation Not Added");
                return;
            }
            AddCompleteAnimations(currentSlide, nextSlide);
        }
        private static void AddCompleteAnimations(PowerPointSlide currentSlide, PowerPointSlide nextSlide)
        {
            var addedSlide = currentSlide.CreateAutoAnimateSlide() as PowerPointAutoAnimateSlide;
            Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(addedSlide.Index);

            AboutForm progressForm = new AboutForm();
            progressForm.Visible = true;
            addedSlide.MoveMotionAnimation();
            addedSlide.PrepareForAutoAnimate();
            RenameCurrentSlide(currentSlide);
            PrepareNextSlide(nextSlide);
            addedSlide.AddAutoAnimation(currentSlideShapes, nextSlideShapes, matchingShapeIDs);
            Globals.ThisAddIn.Application.CommandBars.ExecuteMso("AnimationPreview");
            AddAckSlide();
            progressForm.Visible = false;
        }

        private static void PrepareNextSlide(PowerPointSlide nextSlide)
        {
            if (nextSlide.Transition.EntryEffect != PowerPoint.PpEntryEffect.ppEffectFade && nextSlide.Transition.EntryEffect != PowerPoint.PpEntryEffect.ppEffectFadeSmoothly)
                nextSlide.Transition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;

            if (nextSlide.Name.StartsWith("PPTAutoAnimateSlideStart") || nextSlide.Name.StartsWith("PPTAutoAnimateSlideMulti"))
                nextSlide.Name = "PPTAutoAnimateSlideMulti" + DateTime.Now.ToString("yyyyMMddHHmmssffff");
            else
                nextSlide.Name = "PPTAutoAnimateSlideEnd" + DateTime.Now.ToString("yyyyMMddHHmmssffff");
        }

        private static void RenameCurrentSlide(PowerPointSlide currentSlide)
        {
            if (currentSlide.Name.StartsWith("PPTAutoAnimateSlideEnd") || currentSlide.Name.StartsWith("PPTAutoAnimateSlideMulti"))
                currentSlide.Name = "PPTAutoAnimateSlideMulti" + DateTime.Now.ToString("yyyyMMddHHmmssffff");
            else
                currentSlide.Name = "PPTAutoAnimateSlideStart" + DateTime.Now.ToString("yyyyMMddHHmmssffff");
        }

        private static bool GetMatchingShapeDetails(PowerPointSlide currentSlide, PowerPointSlide nextSlide)
        {
            currentSlideShapes = new PowerPoint.Shape[currentSlide.Shapes.Count];
            nextSlideShapes = new PowerPoint.Shape[currentSlide.Shapes.Count];
            matchingShapeIDs = new int[currentSlide.Shapes.Count];

            int counter = 0;
            PowerPoint.Shape tempMatchingShape = null;
            bool flag = false;
            
            foreach (PowerPoint.Shape sh in currentSlide.Shapes)
            {
                tempMatchingShape = nextSlide.GetShapeWithSameIDAndName(sh);
                if (tempMatchingShape == null)
                    tempMatchingShape = nextSlide.GetShapeWithSameName(sh);
                
                if (tempMatchingShape != null)
                {
                    currentSlideShapes[counter] = sh;
                    nextSlideShapes[counter] = tempMatchingShape;
                    matchingShapeIDs[counter] = sh.Id;
                    counter++;
                    flag = true;
                }
            }

            return flag;
        }

        private static void AddAckSlide()
        {
            try
            {
                PowerPointSlide lastSlide = PowerPointPresentation.Slides.Last();
                if (!lastSlide.isAckSlide())
                {
                    lastSlide.CreateAckSlide();
                }
            }
            catch (Exception e)
            {
                //LogException(e, "AddAckSlide");
                throw;
            }
        }
    }
}
