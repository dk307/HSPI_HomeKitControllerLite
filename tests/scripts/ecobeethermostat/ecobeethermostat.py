import logging
import signal
import argparse
import uuid
import json

from pyhap.accessory import Accessory, Bridge
from pyhap.service import Service
from pyhap.characteristic import Characteristic
from pyhap.accessory_driver import AccessoryDriver
import pyhap.loader as loader

from pyhap.characteristic import (
    HAP_FORMAT_UINT8,
    HAP_FORMAT_INT,
    HAP_PERMISSION_READ,
    PROP_FORMAT,
    PROP_PERMISSIONS,
    PROP_VALID_VALUES,
)
from pyhap.const import (
    CATEGORY_THERMOSTAT,
    HAP_REPR_AID,
    HAP_REPR_CHARS,
    HAP_REPR_IID,
    HAP_REPR_STATUS,
    HAP_REPR_VALUE,
    HAP_SERVER_STATUS,
)

logging.basicConfig(level=logging.DEBUG, format="[%(module)s] %(message)s")

class EcobeeThermostat(Accessory):
    category = CATEGORY_THERMOSTAT

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)

        ecobee_json = EcobeeThermostat._read_file("ecobee.json")

        # thermostat 
        thermostat_service = self.driver.loader.get_service('Thermostat')
        for char_dict in ecobee_json["thermostatCharacteristics"]:
            char = Characteristic.from_dict("Char", char_dict)
            thermostat_service.add_characteristic(char)
           
        self.add_service(thermostat_service)
        self.set_primary_service(thermostat_service)

        # motion sensor
        motion_sensor_service = self.driver.loader.get_service('MotionSensor')
        for char_dict in ecobee_json["motionSensorCharacteristics"]:
            char = Characteristic.from_dict("Char1", char_dict)
            motion_sensor_service.add_characteristic(char)
           
        self.add_service(motion_sensor_service)


        # occupancy sensor
        occupancy_sensor_service = self.driver.loader.get_service('OccupancySensor')
        for char_dict in ecobee_json["occupancySensorCharacteristics"]:
            char = Characteristic.from_dict("Char1", char_dict)
            occupancy_sensor_service.add_characteristic(char)
           
        self.add_service(occupancy_sensor_service)

    def set_target_temperature(self, value):
        logger.info("Set Target Temperature: %s", value)

    def set_target_display_units(self, value):
        logger.info("Set Target Display Units: %s", value)

    def set_target_heating_cooling_state(self, value):
        logger.info("Set Heating Cooling State: %s", value)

    def set_target_relative_humidity(self, value):
        logger.info("Set Target Relative Humidity: %s", value)

    def set_cooling_threshold_temperature(self, value):
        logger.info("Set Cooling threshold Temperature: %s", value)

    def set_heating_threshold_temperature(self, value):
        logger.info("Set Heating threshold Temperature: %s", value)

    def set_target_fan_state(self, value):
        logger.info("Set Target Fan State: %s", value)

    @staticmethod
    def _read_file(path):
        """Read file and return a dict."""
        with open(path, "r") as file:
            return json.load(file)

def get_accessory(driver):
    return EcobeeThermostat(driver, 'EcoBee', aid=1)
    
parser = argparse.ArgumentParser()
parser.add_argument("pincode")
parser.add_argument("persist_file")
args = parser.parse_args()

driver = AccessoryDriver(pincode=args.pincode.encode("UTF-8"), persist_file=args.persist_file)
driver.add_accessory(accessory=get_accessory(driver))
signal.signal(signal.SIGTERM, driver.signal_handler)
driver.start()
