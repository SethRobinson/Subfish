<?php

$latestVersion = 1.09;

if (isset($_GET["version"]))
{
	if (floatval($_GET["version"]) < $latestVersion)
	{
		echo '<p><h2>Version '.$latestVersion.' of Subfish has been released.  <a href="/subfish/SubfishWindows.zip">Download</a> now.<h2></p>'; 
	   exit();
	}
}
header("Location: https://twitter.com/rtsoft");
exit();
?>

