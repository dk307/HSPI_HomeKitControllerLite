import logging
import signal
import random
import argparse

from pyhap.accessory import Accessory, Bridge
from pyhap.accessory_driver import AccessoryDriver
import pyhap.loader as loader
from pyhap.const import CATEGORY_SENSOR

logging.basicConfig(level=logging.DEBUG, format="[%(module)s] %(message)s")

class HumiditySensor(Accessory):
    category = CATEGORY_SENSOR

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)

        serv_temp = self.add_preload_service('HumiditySensor')
        self.char_temp = serv_temp.configure_char('CurrentRelativeHumidity', value=22)

def get_humidity_accessory(driver):
    return HumiditySensor(driver, 'HumiditySensor', aid=34534)
    
parser = argparse.ArgumentParser()
parser.add_argument("pincode")
parser.add_argument("persist_file")
args = parser.parse_args()

driver = AccessoryDriver(pincode=args.pincode.encode("UTF-8"), persist_file=args.persist_file)
bridge = Bridge(driver, "Bridge")

bridge.add_accessory(get_humidity_accessory(driver))

driver.add_accessory(bridge)

signal.signal(signal.SIGTERM, driver.signal_handler)
driver.start()
