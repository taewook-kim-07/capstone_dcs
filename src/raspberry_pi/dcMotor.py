# -*- coding: utf8 -*-

import RPi.GPIO as GPIO

#### 모터 상태
STOP = 0
FORWARD = 1
BACKWARD = 2

class DCMotor:
    global STOP, FORWARD, BACKWARD
    def __init__(self, EN, INA, INB):
        self.EN = EN
        self.INA = INA
        self.INB = INB
        
        GPIO.setup(self.EN, GPIO.OUT, initial=GPIO.LOW)
        GPIO.setup(self.INA, GPIO.OUT, initial=GPIO.LOW)
        GPIO.setup(self.INB, GPIO.OUT, initial=GPIO.LOW)
        # 100khz 로 PWM 동작 시킴 
        self.motor=GPIO.PWM(self.EN, 100)
        self.motor.start(0)

    def __setMotorContorl(self, PWM, INA, INB, speed, stat):
        #모터 속도 제어 PWM
        self.motor.ChangeDutyCycle(speed)

        if stat == FORWARD:
            GPIO.output(INA, GPIO.HIGH)
            GPIO.output(INB, GPIO.LOW)
        elif stat == BACKWARD:
            GPIO.output(INA, GPIO.LOW)
            GPIO.output(INB, GPIO.HIGH)
        elif stat == STOP:
            GPIO.output(INA, GPIO.LOW)
            GPIO.output(INB, GPIO.LOW)

    # 모터 제어함수 간단하게 사용하기 위해 한번더 래핑(감쌈)
    def setMotor(self, speed, stat):
        self.__setMotorContorl(self.EN, self.INA, self.INB, speed, stat)