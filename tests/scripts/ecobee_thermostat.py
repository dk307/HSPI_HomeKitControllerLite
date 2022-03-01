import logging
import signal
import argparse
import uuid

from pyhap.accessory import Accessory, Bridge
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

class ThermostatSensor(Accessory):
    category = CATEGORY_THERMOSTAT

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)

        service_thermostat = self.add_preload_service(
            'Thermostat', ['Name',
                           'CurrentTemperature',
                           'TargetTemperature',
                           'TemperatureDisplayUnits',
                           'TargetHeatingCoolingState',
                           'CurrentHeatingCoolingState',
                           'CurrentRelativeHumidity',
                           'TargetRelativeHumidity',
                           'CoolingThresholdTemperature',
                           'HeatingThresholdTemperature',
                           'CurrentFanState',
                           'TargetFanState'],
        )

        self.set_primary_service(service_thermostat)

        # Predefined
        self.char_current_temp = service_thermostat.configure_char('CurrentTemperature', value=23)
        self.char_target_temp = service_thermostat.configure_char('TargetTemperature', value=20, setter_callback=self.set_target_temperature)
        self.char_target_display_units = service_thermostat.configure_char('TemperatureDisplayUnits', value=1, setter_callback=self.set_target_display_units)
        self.char_target_heating_cooling_state = service_thermostat.configure_char('TargetHeatingCoolingState', value=2, setter_callback=self.set_target_heating_cooling_state)
        self.char_current_heating_cooling_state = service_thermostat.configure_char('CurrentHeatingCoolingState', value=0)
        self.char_current_relative_humidity = service_thermostat.configure_char('CurrentRelativeHumidity', value=23)
        self.char_target_relative_humidity = service_thermostat.configure_char('TargetRelativeHumidity', value=43, setter_callback=self.set_target_relative_humidity)
        self.char_cooling_threshold_temp = service_thermostat.configure_char('CoolingThresholdTemperature', value=23, setter_callback=self.set_cooling_threshold_temperature)
        self.char_currrent_fan_state = service_thermostat.configure_char('CurrentFanState', value=0)
        self.char_target_fan_state = service_thermostat.configure_char('TargetFanState', value=1, setter_callback=self.set_target_fan_state)

        # custom one - Current Program
        current_program_props = {
            PROP_FORMAT: HAP_FORMAT_UINT8,
            PROP_PERMISSIONS: HAP_PERMISSION_READ,
            #PROP_VALID_VALUES: [0, 1, 2]
        }

        self.char_current_program = Characteristic(
            'CurrentProgram',
            uuid.UUID('{b7ddb9a3-54bb-4572-91d2-f1f5b0510f8c}'),
            current_program_props,
        )
        service_thermostat.broker = self
        service_thermostat.add_characteristic(self.char_current_program)


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

def get_accessory(driver):
    return ThermostatSensor(driver, 'EcoBee', aid=1)
    
parser = argparse.ArgumentParser()
parser.add_argument("pincode")
parser.add_argument("persist_file")
args = parser.parse_args()

driver = AccessoryDriver(pincode=args.pincode.encode("UTF-8"), persist_file=args.persist_file)
driver.add_accessory(accessory=get_accessory(driver))
signal.signal(signal.SIGTERM, driver.signal_handler)
driver.start()
