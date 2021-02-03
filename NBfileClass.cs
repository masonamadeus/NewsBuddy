﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace NewsBuddy
{

    public class NBfile // maybe derive from : EventArgs
    {

        public string NBName { get; set; }
        public string NBPath { get; set; }

        public bool NBisSounder = false;

        MainWindow homeBase = Application.Current.Windows[0] as MainWindow;




        public void NBPlay(bool isSounder)
        {
            if (isSounder)
            {
                if (homeBase.SoundersPlayer.Source != null)
                {
                    homeBase.SoundersPlayer.Stop();
                    
                    if (homeBase.SoundersPlayer.Source != new Uri(NBPath))
                    {
                        homeBase.SoundersPlayer.Source = new Uri(NBPath);
                        homeBase.SoundersPlayer.Play();
                    } else 
                    {
                        homeBase.SoundersPlayer.Source = null;
                    }
                    
                }
                    else
                    {
                        homeBase.SoundersPlayer.Source = new Uri(NBPath);
                        homeBase.SoundersPlayer.Play();
                    }
            }
            else
            {
                if (homeBase.ClipsPlayer.Source != null)
                {
                    homeBase.ClipsPlayer.Stop();
                    if (homeBase.ClipsPlayer.Source != new Uri(NBPath))
                    {
                        homeBase.ClipsPlayer.Source = new Uri(NBPath);
                        homeBase.ClipsPlayer.Play();
                    } else
                    {
                        homeBase.ClipsPlayer.Source = null;
                    }
                    
                } 
                else
                {
                    homeBase.ClipsPlayer.Source = new Uri(NBPath);
                    homeBase.ClipsPlayer.Play();
                }
            }
        }

        public Button NBbutton()
        {
            var bc = new BrushConverter();
            Button NBbutton = new Button();
            NBbutton.Content = NBName;
            if (NBisSounder)
            {
                NBbutton.Background = (Brush)bc.ConvertFrom("#05B8CC");
            } else
            {
                NBbutton.Background = (Brush)bc.ConvertFrom("#6DFF9A");
            }
            NBbutton.Style = (Style)Application.Current.FindResource("btnNB");
            NBbutton.Click += (sender, args) =>
            {
                NBPlay(NBisSounder);
            };

            return NBbutton;
        }
    }

}