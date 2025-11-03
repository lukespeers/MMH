<b>Multi Monitor Helper - a Virtual KVM tool (multi monitor enable/disable helper)</b>

I use this program because I have two computers (work, personal) connected to the same set of 3 external monitors (left monitor, center monitor, right monitor) configuration at my desk. This is mainly because I like using the same setup I have for work, as I do for my personal downtime.

This program allows me to do the following possible configurations:
- Personal computer on the left monitor
- Work computer on laptop + (center monitor, right monitor)
- personal computer on (left, center, right) - work computer only on laptop display

By clicking on a button in the gui I can swap between showing my personal computer on one monitor, and all 3 monitors, which can help when I want to work productively with my full setup on my personal time.
If I run the program also on my work laptop, I can disable the external monitors so the monitors automatically switch to the source from my personal computer.

Of course I could just physically disconnect the monitors when I don't want to use them, but it is slower to do that, and can can be a pain when the ass when my work laptop is connected through a dock and requires it for charging.


I based this project off of the post from here:
https://superuser.com/questions/1397941/how-to-turn-off-screen-with-powershell

using ps tool "DisplayConfig"
https://www.powershellgallery.com/packages/DisplayConfig/3.2

<b>Note: this was an experiment and it was successful in solving my multi-monitor situation. There is so much room for improvement, redesign, but for now it works for me as is. I will update this if I have time and make positive changes to the app.</b>

<b>Useage</b>
To use it, clone the repository, build and run the program. You'll see two buttons (one for Left Monitor, one for 3 monitors). These are assigned to run bat/powershell scripts in the /scripts folder. If the program is disabling the wrong monitor, then update the monitor number you want to in there.
Essentially the button click runs a bat script which runs a powershell script because that was how I had to get it working the first time. I would have loved to just run the powershell script but it didn't work for me, so I had to do a work around. 

Note: Once you install "DisplayConfig powershell You can also just run these enable/disable monitor commands straight from powershellscripts, but I wanted buttons and a easy to look at UI. The scripts contain simple commands.
e.g:
- Enable-Display 2
- Set-DisplayPrimary 2
- Disable-Display 1
- Disable-Display 3
