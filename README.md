# Homekit Controller Lite Plugin
Homeseer 4 plugin to add and control IP based homekit devices directly without intermediary. This plugin acts as a homekit controller.
It does not intend to be as complete and polished as Apple's homekit controller. It creates devices in the homeseer corresponding to accessories & characteristics exposed by the homekit device.


## Build State

[![Build Release](https://github.com/dk307/HSPI_HomeKitControllerLite/actions/workflows/buildrelease.yml/badge.svg)](https://github.com/dk307/HSPI_HomeKitControllerLite/actions/workflows/buildrelease.yml)
[![Unit Tests](https://github.com/dk307/HSPI_HomeKitControllerLite/actions/workflows/tests.yml/badge.svg)](https://github.com/dk307/HSPI_HomeKitControllerLite/actions/workflows/tests.yml)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=dk307_HSPI_HomeKitController&metric=coverage)](https://sonarcloud.io/summary/new_code?id=dk307_HSPI_HomeKitController)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=dk307_HSPI_HomeKitController&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=dk307_HSPI_HomeKitController)

## Features

* Pair and unpair IP based homekit devices
* Supports setting values in homekit devices via homeseer devices.
* Listens to the events in the homekit devices and updates the corresponding homeseer devices & features.
* Select which characteristics show up as feature in homeseer devices.

## Tested With
* Ecobee Thermostat
* Philips Hue
* Various open source homekit servers.
