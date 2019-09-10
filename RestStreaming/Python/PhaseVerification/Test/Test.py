import matplotlib.pyplot as plt
from scipy.fftpack import fft
from scipy.signal import hilbert, square, sawtooth, correlate
import numpy as np
from numpy import pi, random
import  cmath


f = 1024    # Signal ferquency
T = 1/f     # Signal period 
N = 5 * f   # Number of samples


phaseShift = np.deg2rad(25)
print("Inserted phase shift: ",round(np.rad2deg(phaseShift),4), " Deg    (", round(phaseShift,4), " Rad)")
print ("\n------------------------------------------------")
print ("               Recovered phase")
print ("------------------------------------------------")


t_stop = 3* T   # Signal length in time akse
t = np.linspace(0.0, t_stop, N)
signal1 = np.sin(2*pi*f*t)
signal2 = np.sin(2*pi*f*t + phaseShift)

plt.plot(t,signal1)
plt.plot(t, signal2)

# calculate phase shift using Hilbert method 
x1h = hilbert(signal1)
x2h = hilbert(signal2)
c = np.inner( x1h, np.conj(x2h) ) / cmath.sqrt( np.inner(x1h,np.conj(x1h)) * np.inner(x2h,np.conj(x2h)) )
phaseRad = round(np.angle(c), 4)            # Calc angle and round it to 4 digits
phaseDeg = round(np.rad2deg(phaseRad), 4)   # Convert to degree
print ("Hilbert method:           ", phaseDeg, " Deg   (", phaseRad, "Rad)"  )

# calculate phase shift using Cross correlation method
xcorr = correlate(signal1, signal2)
dt = np.linspace(-t[-1], t[-1], 2*N-1)
recoverShiftTime = dt[xcorr.argmax()]
recoverShift=2*pi*(((0.5 + recoverShiftTime/T) % 1.0) -0.5)

phaseRad = round(recoverShift, 4)
phaseDeg = round(np.rad2deg(phaseRad), 4)
print ("Cross correlation method: ", phaseDeg, " Deg   (", phaseRad, "Rad)"  )
print("Time shift =", recoverShiftTime, " sec")
plt.grid()
plt.show()
