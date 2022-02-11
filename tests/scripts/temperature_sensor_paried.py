import logging
import signal
import random
import argparse

from pyhap.accessory import Accessory, Bridge
from pyhap.accessory_driver import AccessoryDriver
import pyhap.loader as loader
from pyhap.const import CATEGORY_SENSOR

logging.basicConfig(level=logging.DEBUG, format="[%(module)s] %(message)s")

class TemperatureSensor(Accessory):
    category = CATEGORY_SENSOR

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)

        serv_temp = self.add_preload_service('TemperatureSensor')
        self.char_temp = serv_temp.configure_char('CurrentTemperature')

def get_accessory(driver):
    return TemperatureSensor(driver, 'Sensor1')
    
parser = argparse.ArgumentParser()
parser.add_argument("port", type=int)
parser.add_argument("address")
parser.add_argument("persist_file")
args = parser.parse_args()

driver = AccessoryDriver(port=args.port, address=args.address, persist_file=args.persist_file)
driver.add_accessory(accessory=get_accessory(driver))
signal.signal(signal.SIGTERM, driver.signal_handler)
driver.start()
