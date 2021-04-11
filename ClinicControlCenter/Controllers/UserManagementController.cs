﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClinicControlCenter.Domain.DTOs;
using ClinicControlCenter.Domain.Filters;
using ClinicControlCenter.Domain.Models;
using ClinicControlCenter.Domain.ViewModels;
using ClinicControlCenter.Security;
using Microsoft.AspNetCore.Identity;
using SDK.EntityRepository;
using SDK.EntityRepository.Implementations;
using SDK.EntityRepository.Modules.AutoMapper;
using SDK.EntityRepository.Modules.Pagination;
using SDK.Pagination;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ClinicControlCenter.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserManagementController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<User> _userManager;
        private readonly IMapper _mapper;
        private readonly IEntityRepository<User> _userRepository;
        private readonly IEntityRepository<Patient> _patientRepository;
        private readonly IEntityRepository<Employee> _employeeRepository;
        private readonly IEntityRepository<Doctor> _doctorRepository;


        public UserManagementController(RoleManager<IdentityRole> roleManager,
                                        UserManager<User> userManager,
                                        IMapper mapper,
                                        IEntityRepository<User> userRepository,
                                        IEntityRepository<Patient> patientRepository,
                                        IEntityRepository<Employee> employeeRepository,
                                        IEntityRepository<Doctor> doctorRepository)
        {
            _roleManager        = roleManager;
            _userManager        = userManager;
            _mapper             = mapper;
            _userRepository     = userRepository;
            _patientRepository  = patientRepository;
            _employeeRepository = employeeRepository;
            _doctorRepository   = doctorRepository;
        }

        [HttpGet]
        public async Task<ActionResult<PaginationResult<UserViewModel>>> Get([FromQuery] UserFilter filter)
        {
            // TODO: Filter
            var paginatedValues = await _userRepository
                                        .Mapper<User, UserViewModel>(_mapper)
                                        .FindAllPaginated(filter);

            return Ok(paginatedValues);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserViewModel>> Get(string id)
        {
            return Ok(await _userRepository.Find(id, x => x.Doctor, x => x.Employee, x => x.Patient));
        }

        // TODO: Everything
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{ }

        // TODO: Everything
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{ }

        // TODO: Everything
        // DELETE api/<UserManagementController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{ }

        [HttpPut("to-patient/{id}")]
        public async Task<ActionResult<User>> ToPatient([FromRoute] string id, [FromBody] UserRoleDTO userRoleDto)
        {
            return await SetupUserRoles(id, SecurityConfig.PATIENT_ROLE, userRoleDto);
        }

        [HttpPut("to-employee/{id}")]
        public async Task<ActionResult<User>> ToEmployee([FromRoute] string id, [FromBody] UserRoleDTO userRoleDto)
        {
            return await SetupUserRoles(id, SecurityConfig.EMPLOYEE_ROLE, userRoleDto);
        }

        [HttpPut("to-doctor/{id}")]
        public async Task<ActionResult<User>> ToDoctor([FromRoute] string id, [FromBody] UserRoleDTO userRoleDto)
        {
            return await SetupUserRoles(id, SecurityConfig.DOCTOR_ROLE, userRoleDto);
        }

        [HttpPut("to-manager/{id}")]
        public async Task<ActionResult<User>> ToManager([FromRoute] string id, [FromBody] UserRoleDTO userRoleDto)
        {
            return await SetupUserRoles(id, SecurityConfig.MANAGER_ROLE, userRoleDto);
        }

        private async Task<ActionResult<User>> SetupUserRoles(string userId, string roleToAddOrMaintain = null,
                                                              UserRoleDTO userRoleDto = null)
        {
            var user = await _userRepository.Find(userId, x => x.Doctor, x => x.Employee, x => x.Patient);
            if (user == null)
                return BadRequest("No user for this ID");

            var rolesToRemove = GetRolesToRemove(roleToAddOrMaintain);
            await RemoveRoles(user, rolesToRemove.ToArray());

            if (!string.IsNullOrEmpty(roleToAddOrMaintain))
            {
                user = await AddRoles(user, roleToAddOrMaintain, userRoleDto);
            }

            return Ok(user);
        }

        private async Task<User> AddRoles(User user, string roleToAdd, UserRoleDTO userRoleDto)
        {
            if (!SecurityConfig.ROLES.Contains(roleToAdd))
                return user;

            userRoleDto ??= new UserRoleDTO();

            userRoleDto.Id = user.Id;

            if (roleToAdd == SecurityConfig.PATIENT_ROLE)
            {
                var patient = _mapper.Map<Patient>(userRoleDto);
                patient = await _patientRepository.AddOrUpdate(patient);
                user.Patient = patient;
            }

            if (new[]
                {
                    SecurityConfig.MANAGER_ROLE,
                    SecurityConfig.DOCTOR_ROLE,
                    SecurityConfig.EMPLOYEE_ROLE
                }
                .Contains(roleToAdd))
            {
                var employee = _mapper.Map<Employee>(userRoleDto);
                employee = await _employeeRepository.AddOrUpdate(employee);
                user.Employee = employee;

                if (roleToAdd == SecurityConfig.DOCTOR_ROLE)
                {
                    var doctor = _mapper.Map<Doctor>(userRoleDto);
                    doctor = await _doctorRepository.AddOrUpdate(doctor);
                    user.Doctor = doctor;
                }
            }

            //var updatedUser = await _userRepository.Update(user);

            //if (updatedUser != null)
            //{
            //    user = updatedUser;
            //}

            await _userManager.AddToRoleAsync(user, roleToAdd);

            return user;
        }

        private IEnumerable<string> GetRolesToRemove(string roleToMaintain = null)
        {
            if (roleToMaintain == SecurityConfig.PATIENT_ROLE)
                return new List<string>();

            var parentRoles = SecurityConfig.GetParentRoles(roleToMaintain)
                                            .ToList();

            if (parentRoles.Contains(SecurityConfig.PATIENT_ROLE))
                parentRoles.Remove(SecurityConfig.PATIENT_ROLE);

            return parentRoles;
        }

        private async Task RemoveRoles(User user, string[] rolesToRemove)
        {
            if (rolesToRemove.Contains(SecurityConfig.DOCTOR_ROLE) && user.Doctor != null)
            {
                await _doctorRepository.Remove(user.Doctor);
                user.Doctor = null;
            }

            if (rolesToRemove.Contains(SecurityConfig.EMPLOYEE_ROLE) && user.Employee != null)
            {
                await _employeeRepository.Remove(user.Employee);
                user.Employee = null;
            }

            if (rolesToRemove.Contains(SecurityConfig.PATIENT_ROLE) && user.Patient != null)
            {
                await _patientRepository.Remove(user.Patient);
                user.Patient = null;
            }

            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }
    }
}