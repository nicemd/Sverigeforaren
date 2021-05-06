window.addEventListener("load", function()
{
    // Wrap gps position as a link to Google maps
    var gpsElements = document.getElementsByClassName("gps");
    for(var i=0; i<gpsElements.length; i++){
        var pos = gpsElements[i].innerHTML.split(',');
        if(pos.length==2){
            var a = "<a href='https://www.google.se/maps/search/" + pos[0] + "," + pos[1] + "' target='_blank'>" + gpsElements[i].innerHTML + "</a>";
            gpsElements[i].innerHTML = a;
        }
    }
});
