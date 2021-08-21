"""
Seth's script to fix audio sync problems after importing an EDL in DaVinci Resolve
- Works in free or pay version of Resolve
- Easiest way is probably to copy this to your %PROGRAMDATA%\Blackmagic Design\DaVinci Resolve\Fusion\Scripts\Comp
directory.  After that, the script should show up inside Resolve under scripts, comp.

"""
import sys

if 'resolve' not in globals() and 'resolve' not in locals():
    #----- Create instances, we only need to do this if we're running from the commandline with fuscript
    print ("Detected that we're being run externally.  Setting up.")
    import imp
    lib = 'F:\\DavinciResolveStudio\\fusionscript.dll'
    dvr_script = imp.load_dynamic('fusionscript', lib)
    resolve = dvr_script.scriptapp('Resolve')
    fusion   = dvr_script.scriptapp('Fusion')
    if resolve == None:
         print("Couldn't initialized the resolve object, in Resolve, check DaVinci Resolve->Preferences and under 'General' set external scripting to Local or Network. (requires Studio version to use this!)")
         sys.exit(0)
    #-----

#do the real stuff
pm = resolve.GetProjectManager();
proj = pm.GetCurrentProject();

if proj is None:
    print ('No project is loaded')
    sys.exit(0)
   
mp = proj.GetMediaPool()

tl = proj .GetCurrentTimeline()

if tl is None:
    print ('No timeline is active')
    sys.exit(0)
    
print ('Working on timeline '+tl.GetName())
trackCount = tl.GetTrackCount("video")
print ('Video track count: '+str(trackCount))

newTimelineName = tl.GetName()+'_fixed'
print ('Creating fixed timeline called' +newTimelineName)

videoItems = tl.GetItemListInTrack("video", 1)

#print (proj.GetSetting())
projectFPS = float(proj.GetSetting('timelineFrameRate'))
print("Project framerate: "+str(projectFPS))

index = 0

toAppend = []

for videoItem in videoItems:
    index += 1
    mediaItem =  videoItem.GetMediaPoolItem()
    #info = mediaItem.GetClipProperty()
    #print(info)
    
    FPS = mediaItem.GetClipProperty('FPS')
    modRate = FPS/projectFPS;
    sourceStart = modRate*videoItem.GetLeftOffset();
    sourceEnd = modRate*videoItem.GetRightOffset();
    print('Adding marker '+str(index)+' to '+videoItem.GetName()+' at pos '+str(videoItem.GetStart())+' with '+str(videoItem.GetFusionCompCount())+" fusion items.  Loffset: "+str(videoItem.GetLeftOffset())+" Roffset: "+str(videoItem.GetRightOffset())+' takes: '+str(videoItem.GetTakesCount()))
    #result = videoItem.AddMarker(videoItem.GetLeftOffset() , 'Red', "Test", '', 1.0)
      
    #if not result:
    #print("Error adding marker")
    
    toAppend.append({"mediaPoolItem": mediaItem, "startFrame": sourceStart, "endFrame":sourceEnd})

    #markers = videoItem.GetMarkers()
    #print(str(len(markers))+' markers exist on this clip')
    #print(markers)
    

    
tempTL = mp.CreateEmptyTimeline(newTimelineName)
proj.SetCurrentTimeline(tempTL)

#for thingToAdd in toAppend:
    #print(thingToAdd)

stuffAdded = mp.AppendToTimeline(toAppend)

    
    