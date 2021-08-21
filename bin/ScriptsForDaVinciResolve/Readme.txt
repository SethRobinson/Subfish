Running this script will fix audio sync issues after doing an EDL import in DaVinci Resolve.

This method should work with both the free and pay version:


1.  Copy FixTimelineSync.py to the Resolve scripts directory. It's probably at:
%PROGRAMDATA%\Blackmagic Design\DaVinci Resolve\Fusion\Scripts\Comp

2.  In DaVinci Resolve, choose Workspace->Scripts->Comp->FixTimelineSync

(if FixTimelineSync is missing, you probably copied to the wrong directory!  In my 
experience, you don't have to restart or anything for it to show up)

That's it.  You should notice that the active timeline has been replaced with a new one,
which has the same named with _fixed appended. (if anything broke, you still have the
old one too)







