# gw2client
A desktop application that when open reads an avatar's position from the Gw2 application and sends it to a specified webserver.

This application makes use of the fact that the Guild Wars 2 application posts the currently active avatar's position and some other information (such as the name) to a specific memory location that is normally read by a VOIP program called Mumble. Instead, this application takes the position information and sends it to a specific websocket server, from which it is served up to a map of Guild Wars 2 available online to those with the password, showing the positions of all avatars connected via this application.
