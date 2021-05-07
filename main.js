var map;

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
function initMap() {
    let map = new google.maps.Map(document.getElementById("map"), {
      center: { lat: 62.3875, lng: 16.325556 },
      zoom: 5,
    });

    map.addListener("center_changed", () => {
        window.sessionStorage["map_lat"] = map.center.lat();
        window.sessionStorage["map_lng"] = map.center.lng();
    });
    map.addListener("zoom_changed", () => {
        window.sessionStorage["map_zoom"] = map.zoom;
    });

    var cragElements = document.getElementsByClassName("crag");

    for(var i=0; i<cragElements.length; i++){
        var lat  = cragElements[i].attributes["data-lat"]?.textContent;
        var lon  = cragElements[i].attributes["data-lon"]?.textContent;

        if(lat && lon)
        {            
            let href = cragElements[i].href;
            const contentString ="<div><a href=\"" + href + "\">" + cragElements[i].textContent + "</a></div>";
            const infowindow = new google.maps.InfoWindow({
                content: contentString,
              });

            let marker = new google.maps.Marker({
                position: { lat: Number(lat), lng: Number(lon) },
                map,
                title: cragElements[i].textContent
              });

            marker.addListener('click', ()=>infowindow.open(map, marker));
        }
    }

    if(window.sessionStorage["map_lat"]){
        map.setCenter({lat: Number(window.sessionStorage["map_lat"]), lng: Number(window.sessionStorage["map_lng"]) });        
    }
    if(window.sessionStorage["map_zoom"]){
        map.setZoom(Number(window.sessionStorage["map_zoom"]));

    }
    map.redraw();

  }

