# -*- coding: utf8 -*-
import RPi.GPIO as GPIO
import threading, time

SG_SERVO_MAX_DUTY    = 11   # 서보의 최대(180도) 위치의 주기
SG_SERVO_MIN_DUTY    = 3    # 서보의 최소(0도) 위치의 주기

DR_SERVO_MAX_DUTY    = 12   # 서보의 최대(300도) 위치의 주기
DR_SERVO_MIN_DUTY    = 2    # 서보의 최소(0도) 위치의 주기

class ServoMotor:
    global SERVO_MAX_DUTY, SERVO_MIN_DUTY
    def __init__(self, PIN, TYPE):
        self.PIN = PIN
        self.TYPE = TYPE
        self.TIMER = 0.0
        
        GPIO.setup(self.PIN, GPIO.OUT)
        if self.TYPE == 0:
            self.servo = GPIO.PWM(self.PIN, 50) # 180도 50HZ
        else:
            self.servo = GPIO.PWM(self.PIN, 50) # 300도
        self.servo.start(0)

        __stimer = threading.Thread(target=self.__deAttached, args=())
        __stimer.daemon = True
        __stimer.start()

    def __deAttached(self):
        while True:
            if time.time() - self.TIMER > 0.5:
                GPIO.setup(self.PIN, GPIO.IN)
            time.sleep(0.5)

    def Set(self, degree):
        GPIO.setup(self.PIN, GPIO.OUT)

        if self.TYPE == 0:
            # 각도는 180도를 넘을 수 없다.
            if degree > 180:
                degree = 180

            # 각도(degree)를 duty로 변경한다.
            duty = SG_SERVO_MIN_DUTY+(degree*(SG_SERVO_MAX_DUTY-SG_SERVO_MIN_DUTY)/180.0)
            # duty 값 출력
            print("Degree: {} to {}(Duty)".format(degree, duty))

            # 변경된 duty값을 서보 pwm에 적용
            self.servo.ChangeDutyCycle(duty)

        elif self.TYPE == 1:
            if degree > 300:
                degree = 300
            
            duty = DR_SERVO_MIN_DUTY+(degree*(DR_SERVO_MAX_DUTY-DR_SERVO_MIN_DUTY)/300.0)
            print("Degree: {} to {}(Duty)".format(degree, duty))
            self.servo.ChangeDutyCycle(duty)

        self.TIMER = time.time()