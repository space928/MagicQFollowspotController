# MagicQ Followspot Controller
Ever wanted to use DMX moving spots as followspots? Well now you can using MagicQ Followspot Controller.

This application comes in two parts, a server and a client application. The server talks over OSC to Chamsys MagicQ to receive control signals to turn on/turn off followspots as well as sending metadata about the cue to the followspot operators. The followspot operators use the client application on a separate computer, the clients automatically synchronise with the server application and inform the operator about their cue. The server outputs ArtNet data controlling the followspots, which can be merged back into MagicQ.

This version of the software has been ported to .NET 7 from Derek Mathieson's original code.

## Quick Start

## System Diagram

## Building

